using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;


namespace KreftBimTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ArmadorDeParedes : IExternalCommand
    {
        //listas de erros para exibir ao final dos processos
        private readonly List<string> _errosArmacaoCintas = new List<string>();
        private readonly List<string> _errosArmacaoVergas = new List<string>();
        private const string NomeFamiliaGrauteHorizontal = "Ø 10";

        // Dado leve produzido na fase de leitura (fora da transação) e consumido na fase de escrita (dentro da transação).
        private readonly struct InstrucaoAcoHorizontal
        {
            public InstrucaoAcoHorizontal(Parede parede, XYZ vetorDirecaoExterna, XYZ posicao, double comprimento, ICollection<Wall> paredesIntersectantes)
            {
                Parede = parede;
                VetorDirecaoExterna = vetorDirecaoExterna;
                Posicao = posicao;
                Comprimento = comprimento;
                ParedesIntersectantes = paredesIntersectantes;
            }

            public Parede Parede { get; }
            public XYZ VetorDirecaoExterna { get; }
            public XYZ Posicao { get; }
            public double Comprimento { get; }
            public ICollection<Wall> ParedesIntersectantes { get; }
        }

        // Dado leve para vergas/contravergas — sem dobras, sem paredes intersectantes.
        private readonly struct InstrucaoVerga
        {
            public InstrucaoVerga(Parede parede, XYZ vetorDirecaoExterna, XYZ posicao, double comprimento)
            {
                Parede = parede;
                VetorDirecaoExterna = vetorDirecaoExterna;
                Posicao = posicao;
                Comprimento = comprimento;
            }

            public Parede Parede { get; }
            public XYZ VetorDirecaoExterna { get; }
            public XYZ Posicao { get; }
            public double Comprimento { get; }
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var app = uiApp.Application;
            var uidoc = uiApp.ActiveUIDocument;
            var doc = uidoc.Document;

            TaskDialog armarCintasTaskDialog = new TaskDialog("Armador de Paredes - Fase 1 - Cintas")
            {
                MainInstruction = "Armador de Paredes - Fase 1 - Cintas - DESEJA PROSSEGUIR?",
                MainContent = "Selecione as paredes e os blocos das cintas de amarração. \n" +
                "O comando irá calcular a posição e o comprimento do aço horizontal para cintas de cada parede selecionada.",
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                DefaultButton = TaskDialogResult.No
            };

            if (armarCintasTaskDialog.Show() != TaskDialogResult.Yes)
            {
                return Result.Cancelled;
            }

            //obter o familySymbol do graute horizontal
            FamilySymbol? grauteHorizontalSymbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.Name == NomeFamiliaGrauteHorizontal);

            if (grauteHorizontalSymbol == null)
            {
                TaskDialog.Show("Erro", "Familia de graute horizontal ausente no template");
                return Result.Failed;
            }

            try
            {
                //filtro de seleção de paredes
                TaskDialog.Show("Procedimento", "Selecione as paredes.");
                ElementSelectionFilter wallFilter = new ElementSelectionFilter(e => e is Wall);
                var paredes = uidoc.Selection.PickObjects(ObjectType.Element, wallFilter, "Selecione as paredes")
                    .Select(w => new Parede((Wall)doc.GetElement(w), doc)).ToList();

                //filtro de seleção de blocos para cintas de amarração
                TaskDialog.Show("Procedimento", "Selecione os blocos das cintas de amarração.");
                var referenciasBlocos = uidoc.Selection.PickObjects(ObjectType.Element, new BlocoSelectionFilter(), "Selecione os blocos das cintas de amarração");

                double toleranciaZ = UnitUtils.ConvertToInternalUnits(0.5, UnitTypeId.Centimeters);
                var chavesVistas = new HashSet<double>();
                var coordenadaZdasCintas = new List<double>();

                foreach (var referenciasBloco in referenciasBlocos)
                {
                    Element bloco = doc.GetElement(referenciasBloco);

                    double zOriginal = ((LocationPoint)bloco.Location).Point.Z;

                    double chaveArredondada = Math.Round(zOriginal / toleranciaZ, MidpointRounding.AwayFromZero) * toleranciaZ;

                    if (chavesVistas.Add(chaveArredondada))
                    {
                        coordenadaZdasCintas.Add(chaveArredondada);
                    }
                }

                TaskDialog armarVergasTaskDialog = new TaskDialog("Armador de Paredes - Vergas e Contravergas")
                {
                    MainInstruction = "Armar Vergas e Contravergas - DESEJA PROSSEGUIR?",
                    MainContent = "O comando também pode calcular e instanciar o aço horizontal de vergas (portas e janelas) " +
                    "e contravergas (janelas), com base nos blocos das paredes selecionadas.\n\n" +
                    "Selecione Não para armar apenas as cintas.",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.Yes
                };
                bool armarVergasEContravergas = armarVergasTaskDialog.Show() == TaskDialogResult.Yes;

                //seleção do bloco de referência para detalhamento (DETALHAMENTO / H_DETALHE)
                TaskDialog.Show("Procedimento", "Selecione um bloco de referência para o detalhamento.");
                Reference referenciaBlocoDetalhamento = uidoc.Selection.PickObject(ObjectType.Element, new BlocoSelectionFilter(), "Selecione o bloco de referência para detalhamento");
                Element blocoDetalhamentoElement = doc.GetElement(referenciaBlocoDetalhamento);
                double cotaZDetalhamento = ((LocationPoint)blocoDetalhamentoElement.Location).Point.Z;

                // TODO: avaliar se warm-up dos lazies (WallGeometrySolid, WallBlocosFamilyInstances, WallBlocos) é necessário aqui.
                // Removido/comentado porque nenhum dos três resultados era reaproveitado depois. Se reintroduzir, lembrar
                // que com 100+ paredes / ~30 mil blocos isso deve ser feito parede-a-parede com dispose imediato (ver FASE 1 abaixo),
                // não como uma passagem separada que mantém tudo vivo em memória.
                //foreach (var parede in paredes)
                //{
                //    var solids = parede.WallGeometrySolid;
                //    var familyInstances = parede.WallBlocosFamilyInstances;
                //    var blocos = parede.WallBlocos;
                //}

                // ============================================================
                // FASE 1 — LEITURA E CÁLCULO GEOMÉTRICO (fora da transação)
                // Processa parede por parede. Toda geometria pesada (Solid, GeometryElement)
                // criada para uma parede é disposada antes de seguir para a próxima, para
                // manter o pico de memória controlado em projetos com 100+ paredes.
                // Só dados leves (XYZ, double) sobrevivem para a Fase 2.
                // ============================================================
                var instrucoes = new List<InstrucaoAcoHorizontal>();
                var instrucoesVergas = new List<InstrucaoVerga>();
                double espessura = UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Centimeters);
                double deslocamentoVerticalCinta = UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Centimeters);
                double extensaoContraverga = UnitUtils.ConvertToInternalUnits(15, UnitTypeId.Centimeters);
                var todosBlocosQueIntersectamCintas = new List<FamilyInstance>();

                foreach (var parede in paredes)
                {
                    //Dados da parede
                    var vetorDirecaoExternaParede = parede.WallElement.Orientation;
                    var curvaDaParede = ((LocationCurve)parede.WallElement.Location).Curve;
                    var p0 = curvaDaParede.GetEndPoint(0);
                    var p1 = curvaDaParede.GetEndPoint(1);

                    // Calculado uma única vez por parede — reaproveitado por todas as cintas/instruções dela
                    ICollection<Wall> paredesConectadas = GetWallsConnectedToWall(doc, parede.WallElement);

                    // Sólidos de aberturas (portas/janelas) desta parede.
                    List<(Solid solido, GeometryElement geometriaOrigem)> aberturasData = parede.Portas
                        .Concat(parede.Janelas)
                        .Select(fi => ObterSolidoDeAberturas(fi))
                        .Where(a => a.solido != null)
                        .ToList();

                    List<Solid> solidsAberturas = aberturasData.Select(a => a.solido).ToList();
                    //Retorna null se não houver aberturas, caso contrário retorna um sólido unificado de todas as aberturas.
                    Solid solidoUnido = UnirSolidos(solidsAberturas);

                    //Armar cintas
                    foreach (var coordenadaZdasCinta in coordenadaZdasCintas)
                    {
                        XYZ p0Cinta = new XYZ(p0.X, p0.Y, coordenadaZdasCinta + deslocamentoVerticalCinta);
                        XYZ p1Cinta = new XYZ(p1.X, p1.Y, coordenadaZdasCinta + deslocamentoVerticalCinta);

                        Solid linhaComoSolido = CriarSolidoFaixa(p0Cinta, p1Cinta, espessura);

                        try
                        {
                            var centrosEComprimentos = ObterCentrosEComprimentos(linhaComoSolido, solidoUnido);

                            foreach (var centroEComprimento in centrosEComprimentos)
                            {
                                XYZ posicao = new XYZ(
                                    centroEComprimento.centro.X,
                                    centroEComprimento.centro.Y,
                                    centroEComprimento.centro.Z - deslocamentoVerticalCinta);

                                double comprimentoTruncadoCm = UnitUtils.ConvertToInternalUnits(
                                    Math.Floor(UnitUtils.ConvertFromInternalUnits(centroEComprimento.comprimento, UnitTypeId.Centimeters)),
                                    UnitTypeId.Centimeters);

                                instrucoes.Add(new InstrucaoAcoHorizontal(
                                    parede,
                                    vetorDirecaoExternaParede,
                                    posicao,
                                    comprimentoTruncadoCm,
                                    paredesConectadas));
                            }

                            // Coleta de blocos depende só da cinta (linhaComoSolido), não de haver aço válido — roda sempre.
                            List<FamilyInstance> blocosDestaCinta = ObterBlocosQueIntersectam(doc, linhaComoSolido, parede.VetorDirecaoParede);
                            todosBlocosQueIntersectamCintas.AddRange(blocosDestaCinta);
                        }
                        catch (Exception ex)
                        {
                            _errosArmacaoCintas.Add($"Parede {parede.WallElement.Id}, cota Z {coordenadaZdasCinta}: {ex.Message}");
                        }
                        finally
                        {
                            linhaComoSolido.Dispose();
                        }
                    }

                    Solid solidoQueVeioCru = aberturasData.Count == 1 ? aberturasData[0].solido : null;
                    bool solidoUnidoEhReferenciaCrua = ReferenceEquals(solidoUnido, solidoQueVeioCru);

                    solidoUnido?.Dispose();

                    foreach (var abertura in aberturasData)
                    {
                        bool ehAOrigemDoSolidoCru = solidoUnidoEhReferenciaCrua && ReferenceEquals(abertura.solido, solidoQueVeioCru);
                        if (!ehAOrigemDoSolidoCru)
                        {
                            abertura.geometriaOrigem?.Dispose();
                        }
                    }

                    // ============================================================
                    // VERGAS E CONTRAVERGAS — por porta/janela desta parede.
                    // Só processa se o operador confirmou no gate inicial.
                    // Se nenhum bloco for encontrado na faixa, a abertura é ignorada
                    // (sem instância de aço criada, sem SP_CANALETA alterado).
                    // ============================================================
                    if (!armarVergasEContravergas)
                    {
                        continue;
                    }

                    foreach (var portaFi in parede.Portas)
                    {
                        var porta = new Porta(portaFi);
                        porta.AtualizarDimensoes(14);

                        double extensaoVerga = ObterExtensaoVerga(porta.Comprimento);
                        Solid faixaVerga = CriarFaixaDaAbertura(porta.PortaTransform, porta.Altura, porta.Comprimento, extensaoVerga, espessura);

                        try
                        {
                            var (blocos, comprimentoArmadura) = ObterBlocosEComprimentoDaFaixa(doc, faixaVerga, parede.VetorDirecaoParede);

                            if (blocos.Count > 0 && !AlgumBlocoNaCotaDeCinta(blocos, chavesVistas, toleranciaZ))
                            {
                                XYZ centroVao = new XYZ(
                                    porta.PortaTransform.Origin.X,
                                    porta.PortaTransform.Origin.Y,
                                    porta.PortaTransform.Origin.Z + porta.Altura);

                                XYZ centroVerga = ObterCentroDosBlocos(blocos, parede.VetorDirecaoParede, centroVao);

                                instrucoesVergas.Add(new InstrucaoVerga(parede, vetorDirecaoExternaParede, centroVerga, comprimentoArmadura));
                                todosBlocosQueIntersectamCintas.AddRange(blocos);
                            }
                        }
                        catch (Exception ex)
                        {
                            _errosArmacaoVergas.Add($"Porta {portaFi.Id} (verga): {ex.Message}");
                        }
                        finally
                        {
                            faixaVerga.Dispose();
                        }
                    }

                    foreach (var janelaFi in parede.Janelas)
                    {
                        var janela = new Janela(janelaFi);
                        janela.AtualizarDimensoes(14);

                        // Verga (acima do vão)
                        double extensaoVerga = ObterExtensaoVerga(janela.Comprimento);
                        // Verga — mesma Z de centroVao (Peitoril + Altura)
                        Solid faixaVerga = CriarFaixaDaAbertura(janela.JanelaTransform, janela.Peitoril + janela.Altura, janela.Comprimento, extensaoVerga, espessura);

                        try
                        {
                            var (blocos, comprimentoArmadura) = ObterBlocosEComprimentoDaFaixa(doc, faixaVerga, parede.VetorDirecaoParede);

                            if (blocos.Count > 0 && !AlgumBlocoNaCotaDeCinta(blocos, chavesVistas, toleranciaZ))
                            {
                                XYZ centroVao = new XYZ(
                                    janela.JanelaTransform.Origin.X,
                                    janela.JanelaTransform.Origin.Y,
                                    janela.JanelaTransform.Origin.Z + janela.Peitoril + janela.Altura);

                                XYZ centroVerga = ObterCentroDosBlocos(blocos, parede.VetorDirecaoParede, centroVao);

                                instrucoesVergas.Add(new InstrucaoVerga(parede, vetorDirecaoExternaParede, centroVerga, comprimentoArmadura));
                                todosBlocosQueIntersectamCintas.AddRange(blocos);
                            }
                        }
                        catch (Exception ex)
                        {
                            _errosArmacaoVergas.Add($"Janela {janelaFi.Id} (verga): {ex.Message}");
                        }
                        finally
                        {
                            faixaVerga.Dispose();
                        }

                        // Contraverga — mesma Z de centroVaoCV (Peitoril - 19cm)
                        Solid faixaContraverga = CriarFaixaDaAbertura(janela.JanelaTransform, janela.Peitoril - UnitUtils.ConvertToInternalUnits(19, UnitTypeId.Centimeters), janela.Comprimento, extensaoContraverga, espessura);

                        try
                        {
                            var (blocosCV, comprimentoArmaduraCV) = ObterBlocosEComprimentoDaFaixa(doc, faixaContraverga, parede.VetorDirecaoParede);

                            if (blocosCV.Count > 0 && !AlgumBlocoNaCotaDeCinta(blocosCV, chavesVistas, toleranciaZ))
                            {
                                XYZ centroVaoCV = new XYZ(
                                    janela.JanelaTransform.Origin.X,
                                    janela.JanelaTransform.Origin.Y,
                                    janela.JanelaTransform.Origin.Z + janela.Peitoril - UnitUtils.ConvertToInternalUnits(19, UnitTypeId.Centimeters));

                                XYZ centroContraverga = ObterCentroDosBlocos(blocosCV, parede.VetorDirecaoParede, centroVaoCV);

                                instrucoesVergas.Add(new InstrucaoVerga(parede, vetorDirecaoExternaParede, centroContraverga, comprimentoArmaduraCV));
                                todosBlocosQueIntersectamCintas.AddRange(blocosCV);
                            }
                        }
                        catch (Exception ex)
                        {
                            _errosArmacaoVergas.Add($"Janela {janelaFi.Id} (contraverga): {ex.Message}");
                        }
                        finally
                        {
                            faixaContraverga.Dispose();
                        }
                    }
                }

                // ============================================================
                // FASE 2 — ESCRITA (dentro da transação)
                // Cria as instâncias parede por parede e, ao final de cada parede,
                // ajusta DETALHAMENTO/H_DETALHE de todo o aço horizontal dela.
                // ============================================================
                TransactionStatus transactionStatus;
                using (Transaction tx = new Transaction(doc, "Aço Horizontal"))
                {
                    tx.Start();
                    // Acumula avisos menores(ex.: elementos sobrepostos) para exibir todos juntos ao final,
                    // em vez de interromper a criação em lote com um popup por elemento.
                    tx.SetFailureHandlingOptions(
                        tx.GetFailureHandlingOptions()
                         .SetDelayedMiniWarnings(true));

                    if (!grauteHorizontalSymbol.IsActive)
                    {
                        grauteHorizontalSymbol.Activate();
                        doc.Regenerate();
                    }

                    double deslocamentoDetalhamento = UnitUtils.ConvertToInternalUnits(50, UnitTypeId.Centimeters);

                    foreach (var parede in paredes)
                    {
                        var acosDestaParede = new List<(FamilyInstance fi, XYZ posicao)>();

                        foreach (var instrucao in instrucoes.Where(i => i.Parede.Id == parede.Id))
                        {
                            try
                            {
                                FamilyInstance aco = CriaInstanciaAcoHorizontal(
                                doc,
                                grauteHorizontalSymbol,
                                instrucao.VetorDirecaoExterna,
                                instrucao.Posicao,
                                instrucao.Comprimento,
                                instrucao.ParedesIntersectantes);

                                acosDestaParede.Add((aco, instrucao.Posicao));
                            }
                            catch (Exception ex)
                            {
                                _errosArmacaoCintas.Add($"Parede {instrucao.Parede.Id}: {ex.Message}");
                            }
                        }

                        foreach (var instrucaoVerga in instrucoesVergas.Where(i => i.Parede.Id == parede.Id))
                        {
                            try
                            {
                                FamilyInstance aco = CriaInstanciaAcoHorizontalVerga(
                                doc,
                                grauteHorizontalSymbol,
                                instrucaoVerga.VetorDirecaoExterna,
                                instrucaoVerga.Posicao,
                                instrucaoVerga.Comprimento);

                                acosDestaParede.Add((aco, instrucaoVerga.Posicao));
                            }
                            catch (Exception ex)
                            {
                                _errosArmacaoVergas.Add($"Verga/Contraverga (parede {parede.Id}): {ex.Message}");
                            }
                        }

                        DetalharAcosDaParede(acosDestaParede, cotaZDetalhamento, deslocamentoDetalhamento, toleranciaZ);
                    }

                    foreach (var blocoFamilyInstance in todosBlocosQueIntersectamCintas)
                    {
                        Parameter spCanaleta = blocoFamilyInstance.LookupParameter("SP_CANALETA");

                        if (spCanaleta.AsInteger() == 1)
                            continue;

                        spCanaleta.Set(1);
                    }

                    transactionStatus = tx.Commit();
                }

                if (transactionStatus != TransactionStatus.Committed)
                {
                    _errosArmacaoCintas.Add($"A transação não foi concluída com sucesso (status: {transactionStatus}).");
                }

                var todosOsErros = _errosArmacaoCintas.Concat(_errosArmacaoVergas).ToList();

                TaskDialog armadorConcluido = new TaskDialog("Armador de Paredes - Concluído")
                {
                    MainInstruction = "Armador de Paredes - log de erros",
                    MainContent = $"Erros: {todosOsErros.Count}\n\n{string.Join("\n", todosOsErros)}",
                    CommonButtons = TaskDialogCommonButtons.Ok,
                    DefaultButton = TaskDialogResult.Ok
                };

                armadorConcluido.Show();

                return Result.Succeeded;
            }

            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Usuário cancelou a seleção (ESC) — não é um erro, apenas desistência do fluxo.
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Cria a instância de aço horizontal, aplica a rotação para alinhar com a parede
        /// e seta os parâmetros de comprimento (COMPA/COMPB). Não seta dobras nem detalhamento
        /// específico de cintas — isso é responsabilidade dos métodos chamadores.
        /// </summary>
        private FamilyInstance CriarInstanciaAcoHorizontalBase(
            Document doc,
            FamilySymbol grauteHorizontalSymbol,
            XYZ vetorDirecaoExternaParede,
            XYZ pointInstance,
            double comprimentoAcoHorizontal,
            out Parameter compA,
            out Parameter compB)
        {
            FamilyInstance acoHorizontalInstance = doc.Create.NewFamilyInstance(pointInstance, grauteHorizontalSymbol, StructuralType.NonStructural);
            XYZ acoHorizontalInstanceOrigem = ((LocationPoint)acoHorizontalInstance.Location).Point;
            XYZ facingAtual = acoHorizontalInstance.FacingOrientation;

            // Ângulo de rotação do graute horizontal: AngleTo sempre retorna valor não-negativo,
            // o produto vetorial define o sentido (horário/anti-horário) da rotação.
            double angulo = facingAtual.AngleTo(vetorDirecaoExternaParede);
            // Verifica o sinal do angulo usando o produto cruzado
            XYZ crossProduct = facingAtual.CrossProduct(vetorDirecaoExternaParede);
            if (crossProduct.Z < 0)
                angulo = -angulo;
            ElementTransformUtils.RotateElement(
                doc,
                acoHorizontalInstance.Id,
                Line.CreateBound(acoHorizontalInstanceOrigem, acoHorizontalInstanceOrigem + XYZ.BasisZ), angulo);

            doc.Regenerate(); // força o Revit a recalcular a geometria antes de ler HandOrientation/FacingOrientation

            //Ler Parâmetros
            compA = acoHorizontalInstance.LookupParameter("COMPA");
            compB = acoHorizontalInstance.LookupParameter("COMPB");
            Parameter detalhamento = acoHorizontalInstance.LookupParameter("DETALHAMENTO");

            //Setar Parâmetros de comprimentos
            compA.Set(comprimentoAcoHorizontal / 2);
            compB.Set(comprimentoAcoHorizontal / 2);
            // Valor inicial — será recalculado/sobrescrito por DetalharAcosDaParede ao final da parede.
            detalhamento.Set(1);

            return acoHorizontalInstance;
        }

        /// <summary>
        /// Aço horizontal de cinta — inclui lógica de dobras (checa paredes perpendiculares nas pontas).
        /// </summary>
        public FamilyInstance CriaInstanciaAcoHorizontal(
            Document doc,
            FamilySymbol grauteHorizontalSymbol,
            XYZ vetorDirecaoExternaParede,
            XYZ pointInstance,
            double comprimentoAcoHorizontal,
            ICollection<Wall> paredesIntersectantes)
        {
            FamilyInstance acoHorizontalInstance = CriarInstanciaAcoHorizontalBase(
                doc, grauteHorizontalSymbol, vetorDirecaoExternaParede, pointInstance, comprimentoAcoHorizontal,
                out Parameter compA, out Parameter compB);

            //Setar dobras
            SetarDobras(acoHorizontalInstance,
                paredesIntersectantes,
                comprimentoAcoHorizontal,
                compA,
                compB);

            return acoHorizontalInstance;
        }

        /// <summary>
        /// Aço horizontal de verga/contraverga — sem dobras, já que a extensão de apoio
        /// (15/30cm de cada lado) cumpre esse papel estrutural.
        /// </summary>
        public FamilyInstance CriaInstanciaAcoHorizontalVerga(
            Document doc,
            FamilySymbol grauteHorizontalSymbol,
            XYZ vetorDirecaoExternaParede,
            XYZ pointInstance,
            double comprimentoAcoHorizontal)
        {
            return CriarInstanciaAcoHorizontalBase(
                doc, grauteHorizontalSymbol, vetorDirecaoExternaParede, pointInstance, comprimentoAcoHorizontal,
                out _, out _);
        }

        private Solid CriarSolidoFaixa(XYZ p0, XYZ p1, double espessura)
        {
            XYZ direcao = (p1 - p0).Normalize();
            XYZ perpendicular = direcao.CrossProduct(XYZ.BasisZ).Normalize();

            CurveLoop perfil = new CurveLoop();
            perfil.Append(Line.CreateBound(p0 + perpendicular * espessura, p1 + perpendicular * espessura));
            perfil.Append(Line.CreateBound(p1 + perpendicular * espessura, p1 - perpendicular * espessura));
            perfil.Append(Line.CreateBound(p1 - perpendicular * espessura, p0 - perpendicular * espessura));
            perfil.Append(Line.CreateBound(p0 - perpendicular * espessura, p0 + perpendicular * espessura));

            return GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { perfil },
                XYZ.BasisZ,
                espessura * 2);
        }

        /// <summary>
        /// Cria uma faixa fina horizontal a uma altura Z relativa à origem da abertura (porta/janela).
        /// Serve tanto para verga (alturaZRelativa = Altura, topo do vão) quanto para
        /// contraverga (alturaZRelativa = Peitoril, base do vão).
        /// </summary>
        private Solid CriarFaixaDaAbertura(Transform aberturaTransform, double alturaZRelativa, double comprimento, double extensao, double espessuraFaixa)
        {
            XYZ origem = aberturaTransform.Origin;
            XYZ direcaoAoLongoDoVao = aberturaTransform.BasisX; // assumindo alinhado com a direção da parede

            XYZ centro = new XYZ(origem.X, origem.Y, origem.Z + alturaZRelativa);

            double meiaFaixa = comprimento / 2 + extensao;
            XYZ p0 = centro - direcaoAoLongoDoVao * meiaFaixa;
            XYZ p1 = centro + direcaoAoLongoDoVao * meiaFaixa;

            return CriarSolidoFaixa(p0, p1, espessuraFaixa);
        }

        /// <summary>
        /// Regra construtiva: vãos até 100cm usam 15cm de apoio de cada lado; acima disso, 30cm.
        /// </summary>
        private double ObterExtensaoVerga(double comprimentoVao)
        {
            double comprimentoCm = UnitUtils.ConvertFromInternalUnits(comprimentoVao, UnitTypeId.Centimeters);
            double extensaoCm = comprimentoCm <= 100 ? 15 : 30;
            return UnitUtils.ConvertToInternalUnits(extensaoCm, UnitTypeId.Centimeters);
        }

        /// <summary>
        /// Retorna todos os blocos tocados pela faixa e a soma total dos comprimentos deles —
        /// o comprimento da armadura é sempre o comprimento total dos blocos tocados,
        /// nunca cortado no meio de um bloco.
        /// </summary>
        private (List<FamilyInstance> blocos, double comprimentoArmadura) ObterBlocosEComprimentoDaFaixa(
            Document doc, Solid faixa, XYZ direcaoParede)
        {
            List<FamilyInstance> blocos = ObterBlocosQueIntersectam(doc, faixa, direcaoParede);

            double comprimentoArmadura = blocos
                .Select(fi => new Bloco(fi))
                .Sum(b => UnitUtils.ConvertToInternalUnits(b.BlocoComprimento, UnitTypeId.Centimeters));

            return (blocos, comprimentoArmadura);
        }

        /// <summary>
        /// Calcula o ponto central da extensão real coberta pelos blocos, ao longo da direção da parede.
        /// Não é a média das posições dos blocos (que seria enviesada por tamanhos diferentes), mas o
        /// ponto médio entre a borda mais à esquerda e a borda mais à direita de todos os blocos —
        /// ou seja, o centro geométrico do trecho realmente ocupado por eles.
        /// A cota Z e o deslocamento perpendicular vêm do ponto de referência (posição da abertura),
        /// só a posição ao longo da parede é recalculada a partir dos blocos.
        /// </summary>
        private XYZ ObterCentroDosBlocos(List<FamilyInstance> blocos, XYZ direcaoAoLongoDoVao, XYZ pontoReferencia)
        {
            double minProjecao = double.MaxValue;
            double maxProjecao = double.MinValue;

            foreach (var fi in blocos)
            {
                Bloco bloco = new Bloco(fi);
                XYZ posicaoBloco = bloco.BlocoTransform.Origin;
                double comprimentoBloco = UnitUtils.ConvertToInternalUnits(bloco.BlocoComprimento, UnitTypeId.Centimeters);
                double meioComprimento = comprimentoBloco / 2;

                double projecao = (posicaoBloco - pontoReferencia).DotProduct(direcaoAoLongoDoVao);

                minProjecao = Math.Min(minProjecao, projecao - meioComprimento);
                maxProjecao = Math.Max(maxProjecao, projecao + meioComprimento);
            }

            double centroProjecao = (minProjecao + maxProjecao) / 2;

            return pontoReferencia + direcaoAoLongoDoVao * centroProjecao;
        }

        /// <summary>
        /// Ajusta DETALHAMENTO e H_DETALHE de todas as instâncias de aço horizontal de uma parede
        /// (cintas + vergas + contravergas juntas). Camadas são as cotas Z distintas dessa parede,
        /// da mais alta (camada 1) para a mais baixa. H_DETALHE = Z da instância - Z do bloco de
        /// referência + camada * deslocamento. Duplicatas (mesmo BARRA_COMPR_RETO_TRUNCADO, mesmas
        /// dobras e mesma posição X/Y — só Z diferente, ou seja, a mesma barra repetida em níveis
        /// distintos) têm DETALHAMENTO desligado em todas as cotas abaixo da mais alta do grupo;
        /// H_DETALHE continua sendo calculado e setado normalmente mesmo nesses casos.
        /// </summary>
        private void DetalharAcosDaParede(List<(FamilyInstance fi, XYZ posicao)> acosDaParede, double cotaZDetalhamento, double deslocamento, double toleranciaZCamada)
        {
            if (acosDaParede.Count == 0)
                return;

            // Evita que ruído de ponto flutuante entre caminhos de cálculo diferentes
            // (cinta vs. verga) crie "camadas" falsas para barras fisicamente no mesmo nível.
            double QuantizarZ(double z) => Math.Round(z / toleranciaZCamada, MidpointRounding.AwayFromZero) * toleranciaZCamada;

            // Agrupamento de duplicatas: mesma assinatura (comprimento truncado + dobras) E mesma
            // posição X/Y (com tolerância) — só a cota Z pode diferir dentro do mesmo grupo.
            // Precisa ser calculado ANTES das camadas, pois uma cota Z onde todas as barras
            // acabam desligadas não deve consumir uma camada própria (ver abaixo).
            double toleranciaXY = UnitUtils.ConvertToInternalUnits(0.5, UnitTypeId.Centimeters);
            var gruposPorPosicao = new List<(
                XYZ chaveXY,
                (int comprimento, int d10a, int d10b, int d35a, int d35b, int d35au, int d35bu) chaveAssinatura,
                List<(FamilyInstance fi, XYZ posicao)> itens)>();

            foreach (var aco in acosDaParede)
            {
                var assinatura = ObterAssinaturaDetalhamento(aco.fi);

                int indiceGrupo = gruposPorPosicao.FindIndex(g =>
                    g.chaveAssinatura.Equals(assinatura) &&
                    Math.Abs(g.chaveXY.X - aco.posicao.X) <= toleranciaXY &&
                    Math.Abs(g.chaveXY.Y - aco.posicao.Y) <= toleranciaXY);

                if (indiceGrupo >= 0)
                {
                    gruposPorPosicao[indiceGrupo].itens.Add(aco);
                }
                else
                {
                    gruposPorPosicao.Add((aco.posicao, assinatura, new List<(FamilyInstance fi, XYZ posicao)> { aco }));
                }
            }

            var desligados = new HashSet<ElementId>();
            foreach (var grupo in gruposPorPosicao)
            {
                if (grupo.itens.Count <= 1)
                    continue;

                double zMaximoDoGrupo = grupo.itens.Max(i => i.posicao.Z);

                foreach (var item in grupo.itens)
                {
                    if (item.posicao.Z < zMaximoDoGrupo)
                        desligados.Add(item.fi.Id);
                }
            }

            // Camadas: só cotas Z com pelo menos uma barra ativa (DETALHAMENTO ligado) contam.
            // Uma cota onde todas as barras viraram duplicata desligada não existe como camada —
            // não consome número nem afeta o espaçamento das camadas reais.
            List<double> cotasZAtivas = acosDaParede
                .Where(a => !desligados.Contains(a.fi.Id))
                .Select(a => QuantizarZ(a.posicao.Z))
                .Distinct()
                .OrderByDescending(z => z)
                .ToList();

            foreach (var aco in acosDaParede)
            {
                // Barra desligada (duplicata) não faz nada — nem camada, nem H_DETALHE, só desliga.
                if (desligados.Contains(aco.fi.Id))
                {
                    aco.fi.LookupParameter("DETALHAMENTO").Set(0);
                    continue;
                }

                double zQuantizado = QuantizarZ(aco.posicao.Z);
                int camada = cotasZAtivas.Count(z => z > zQuantizado + 1e-9) + 1;

                double hDetalhe = aco.posicao.Z - cotaZDetalhamento + camada * deslocamento;

                aco.fi.LookupParameter("H_DETALHE").Set(hDetalhe);
                aco.fi.LookupParameter("DETALHAMENTO").Set(1);
            }
        }

        /// <summary>
        /// Assinatura usada para detectar duplicatas: comprimento truncado (arredondado para inteiro,
        /// eliminando pequenas diferenças) e o estado das seis dobras. Duas instâncias com a mesma
        /// assinatura e mesma posição X/Y são consideradas a mesma barra repetida em outra cota Z.
        /// </summary>
        private (int comprimento, int d10a, int d10b, int d35a, int d35b, int d35au, int d35bu) ObterAssinaturaDetalhamento(FamilyInstance fi)
        {
            Parameter barraComprTruncado = fi.LookupParameter("BARRA_COMPR_RETO_TRUNCADO");
            int comprimento = barraComprTruncado != null ? (int)Math.Round(barraComprTruncado.AsDouble()) : 0;

            int ObterInt(string nomeParametro)
            {
                Parameter p = fi.LookupParameter(nomeParametro);
                return p != null ? p.AsInteger() : 0;
            }

            return (
                comprimento,
                ObterInt("DOBRA_10_A"),
                ObterInt("DOBRA_10_B"),
                ObterInt("DOBRA_35_A"),
                ObterInt("DOBRA_35_B"),
                ObterInt("DOBRA_35_AU"),
                ObterInt("DOBRA_35_BU"));
        }

        private (Solid solido, GeometryElement geometriaOrigem) ObterSolidoDeAberturas(Element element)
        {
            var geomOptions = new Options
            {
                ComputeReferences = false,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = true
            };

            GeometryElement geomElement = element.get_Geometry(geomOptions);

            if (geomElement == null) return (null, null);

            foreach (GeometryObject geomObj in geomElement)
            {
                if (geomObj is Solid solid && solid.Volume > 0)
                    return (solid, geomElement);

                if (geomObj is GeometryInstance geomInstance)
                {
                    foreach (GeometryObject instanceObj in geomInstance.GetInstanceGeometry())
                    {
                        if (instanceObj is Solid instanceSolid && instanceSolid.Volume > 0)
                            return (instanceSolid, geomElement);
                    }
                }
            }

            // Nenhum Solid válido encontrado — nada a devolver, descarta aqui mesmo.
            geomElement.Dispose();
            return (null, null);
        }

        private Solid UnirSolidos(IEnumerable<Solid> solidos)
        {
            Solid resultado = null;
            bool resultadoEhIntermediario = false; // true quando 'resultado' veio de ExecuteBooleanOperation (seguro disposar)

            foreach (var solido in solidos)
            {
                if (resultado == null)
                {
                    resultado = solido;
                    resultadoEhIntermediario = false; // este veio "cru" de ObterSolidoDeAberturas, não disposar
                    continue;
                }

                try
                {
                    Solid novoResultado = BooleanOperationsUtils.ExecuteBooleanOperation(
                        resultado,
                        solido,
                        BooleanOperationsType.Union);

                    if (resultadoEhIntermediario)
                        resultado.Dispose();

                    resultado = novoResultado;
                    resultadoEhIntermediario = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Falha ao unir sólidos: {ex.Message}");
                }
            }

            return resultado;
        }

        private List<(XYZ centro, double comprimento)> ObterCentrosEComprimentos(Solid solidoPerfil, Solid solidoAberturas)
        {
            var resultado = new List<(XYZ centro, double comprimento)>();
            const double comprimentoMinimoCm = 15.0;
            double comprimentoMinimo = UnitUtils.ConvertToInternalUnits(comprimentoMinimoCm, UnitTypeId.Centimeters);

            // Assume que a menor face do sólido extrudado é a seção transversal (ponta do perfil),
            // não uma face lateral — válido para o perfil fino gerado em CreateExtrusionGeometry.
            double areaPerfil = solidoPerfil.Faces
                .Cast<Face>()
                .Min(f => f.Area);

            // Sem aberturas: a cinta não precisa ser cortada, usa o perfil inteiro
            if (solidoAberturas == null)
            {
                double comprimentoPerfil = solidoPerfil.Volume / areaPerfil;

                if (comprimentoPerfil >= comprimentoMinimo)
                {
                    XYZ centroPerfil = solidoPerfil.ComputeCentroid();
                    resultado.Add((centroPerfil, comprimentoPerfil));
                }

                return resultado;
            }

            Solid diferenca;
            try
            {
                diferenca = BooleanOperationsUtils.ExecuteBooleanOperation(
                    solidoPerfil,
                    solidoAberturas,
                    BooleanOperationsType.Difference);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Falha ao calcular diferença de sólidos para a cinta: {ex.Message}", ex);
            }

            using (diferenca)
            {
                if (diferenca == null || diferenca.Volume <= 0)
                    return resultado;

                IList<Solid> solidos = SolidUtils.SplitVolumes(diferenca);

                foreach (Solid solido in solidos)
                {
                    double comprimento = solido.Volume / areaPerfil;

                    if (comprimento >= comprimentoMinimo)
                    {
                        XYZ centro = solido.ComputeCentroid();
                        resultado.Add((centro, comprimento));
                    }

                    solido.Dispose();
                }
            }

            return resultado;
        }

        private List<FamilyInstance> ObterBlocosQueIntersectam(Document doc, Solid solidoFino, XYZ direcaoParede)
        {
            BoundingBoxXYZ bbSolido = solidoFino.GetBoundingBox();
            Transform transformSolido = bbSolido.Transform;
            XYZ cantoMin = transformSolido.OfPoint(bbSolido.Min);
            XYZ cantoMax = transformSolido.OfPoint(bbSolido.Max);

            Outline outline = new Outline(
                new XYZ(Math.Min(cantoMin.X, cantoMax.X), Math.Min(cantoMin.Y, cantoMax.Y), Math.Min(cantoMin.Z, cantoMax.Z)),
                new XYZ(Math.Max(cantoMin.X, cantoMax.X), Math.Max(cantoMin.Y, cantoMax.Y), Math.Max(cantoMin.Z, cantoMax.Z)));

            var candidatos = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .WhereElementIsNotElementType()
                .WherePasses(new BoundingBoxIntersectsFilter(outline))
                .WherePasses(new ElementParameterFilter(
                    new FilterStringRule(
                        new ParameterValueProvider(new ElementId(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS)),
                        new FilterStringEquals(), "Bloco")))
                .Cast<FamilyInstance>()
                .ToList();

            var filtroIntersecao = new ElementIntersectsSolidFilter(solidoFino);

            return candidatos
                .Where(fi => filtroIntersecao.PassesFilter(doc, fi.Id))
                .Where(fi =>
                {
                    XYZ blocoHandVector = fi.HandOrientation.Normalize();
                    XYZ crossProduct = blocoHandVector.CrossProduct(direcaoParede);
                    return crossProduct.IsAlmostEqualTo(XYZ.Zero);
                })
                .ToList();
        }

        private ICollection<Wall> GetWallsConnectedToWall(Document document, Wall targetWall)
        {
            LocationCurve targetLocation = targetWall.Location as LocationCurve;
            if (targetLocation == null) return new List<Wall>();

            Curve targetCurve = targetLocation.Curve;

            // Passo 1: expande o BoundingBox da target wall pela tolerância,
            // para garantir que paredes em T sejam capturadas pelo pré-filtro
            BoundingBoxXYZ bb = targetWall.get_BoundingBox(null);
            double tolerance = 0.01;

            Outline outline = new Outline(
                new XYZ(bb.Min.X - tolerance, bb.Min.Y - tolerance, bb.Min.Z - tolerance),
                new XYZ(bb.Max.X + tolerance, bb.Max.Y + tolerance, bb.Max.Z + tolerance)
            );

            BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(outline);

            // Passo 2: candidatos via BoundingBox (quick filter)
            // TODO: se quiser restringir só a paredes "Estrutural", adicionar aqui:
            // .WherePasses(new ElementParameterFilter(
            //     new FilterStringRule(
            //         new ParameterValueProvider(new ElementId(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS)),
            //         new FilterStringEquals(), "Estrutural")))
            IList<Wall> candidatos = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .WherePasses(new ExclusionFilter(new List<ElementId> { targetWall.Id }))
                .WherePasses(bbFilter)
                .Cast<Wall>()
                .ToList();

            XYZ alvoStart = targetCurve.GetEndPoint(0);
            XYZ alvoEnd = targetCurve.GetEndPoint(1);

            // Passo 3: valida se algum endpoint de uma das duas curvas
            // está sobre a curva da outra parede, nos dois sentidos
            return candidatos
                .Where(w =>
                {
                    LocationCurve loc = w.Location as LocationCurve;
                    if (loc == null) return false;

                    Curve candidataCurve = loc.Curve;

                    XYZ candidataStart = candidataCurve.GetEndPoint(0);
                    XYZ candidataEnd = candidataCurve.GetEndPoint(1);

                    // Sentido 1: pontas da candidata tocando a curva da alvo (ex.: T normal)
                    IntersectionResult projCandidataStart = targetCurve.Project(candidataStart);
                    IntersectionResult projCandidataEnd = targetCurve.Project(candidataEnd);
                    bool candidataTocaAlvo =
                        (projCandidataStart != null && projCandidataStart.Distance <= tolerance) ||
                        (projCandidataEnd != null && projCandidataEnd.Distance <= tolerance);

                    // Sentido 2: pontas da alvo tocando a curva da candidata (T invertido)
                    IntersectionResult projAlvoStart = candidataCurve.Project(alvoStart);
                    IntersectionResult projAlvoEnd = candidataCurve.Project(alvoEnd);
                    bool alvoTocaCandidata =
                        (projAlvoStart != null && projAlvoStart.Distance <= tolerance) ||
                        (projAlvoEnd != null && projAlvoEnd.Distance <= tolerance);

                    return candidataTocaAlvo || alvoTocaCandidata;
                })
                .ToList();
        }

        private void SetarDobras(FamilyInstance acoHorizontalInstance,
            ICollection<Wall> paredesIntersectantes,
            double comprimentoAcoHorizontal,
            Parameter compA, Parameter compB)
        {
            var compAValue = comprimentoAcoHorizontal / 2;
            var compBValue = comprimentoAcoHorizontal / 2;
            var aumentoDeComp = UnitUtils.ConvertToInternalUnits(5, UnitTypeId.Centimeters);

            Parameter DOBRA_10_A = acoHorizontalInstance.LookupParameter("DOBRA_10_A");
            Parameter DOBRA_10_B = acoHorizontalInstance.LookupParameter("DOBRA_10_B");
            Parameter DOBRA_35_A = acoHorizontalInstance.LookupParameter("DOBRA_35_A");
            Parameter DOBRA_35_B = acoHorizontalInstance.LookupParameter("DOBRA_35_B");
            Parameter DOBRA_35_AU = acoHorizontalInstance.LookupParameter("DOBRA_35_AU");
            Parameter DOBRA_35_BU = acoHorizontalInstance.LookupParameter("DOBRA_35_BU");

            LocationPoint locationPoint = acoHorizontalInstance.Location as LocationPoint;
            XYZ origem = locationPoint.Point;

            XYZ direcaoEixo = acoHorizontalInstance.HandOrientation.Normalize();
            XYZ perpendicular = acoHorizontalInstance.FacingOrientation.Normalize();

            XYZ pontaA = origem - direcaoEixo * compAValue;
            XYZ pontaB = origem + direcaoEixo * compBValue;

            // Ponta A — testa dir1, se não achar testa dir2, para na primeira que encontrar
            if (HaParedeNaDirecao(pontaA, perpendicular, paredesIntersectantes))
            {
                DOBRA_35_A.Set(0);
                DOBRA_35_AU.Set(1);
                DOBRA_10_A.Set(0);
                compA.Set(compAValue + aumentoDeComp);
            }
            else if (HaParedeNaDirecao(pontaA, -perpendicular, paredesIntersectantes))
            {
                DOBRA_35_A.Set(1);
                DOBRA_35_AU.Set(0);
                DOBRA_10_A.Set(0);
                compA.Set(compAValue + aumentoDeComp);
            }
            else
            {
                DOBRA_35_A.Set(0);
                DOBRA_35_AU.Set(0);
                DOBRA_10_A.Set(1);
            }

            // Ponta B — mesma lógica
            if (HaParedeNaDirecao(pontaB, perpendicular, paredesIntersectantes))
            {
                DOBRA_35_B.Set(0);
                DOBRA_35_BU.Set(1);
                DOBRA_10_B.Set(0);
                compB.Set(compBValue + aumentoDeComp);
            }
            else if (HaParedeNaDirecao(pontaB, -perpendicular, paredesIntersectantes))
            {
                DOBRA_35_B.Set(1);
                DOBRA_35_BU.Set(0);
                DOBRA_10_B.Set(0);
                compB.Set(compBValue + aumentoDeComp);
            }
            else
            {
                DOBRA_35_B.Set(0);
                DOBRA_35_BU.Set(0);
                DOBRA_10_B.Set(1);
            }
        }

        private bool HaParedeNaDirecao(XYZ ponta, XYZ direcao, ICollection<Wall> paredesIntersectantes)
        {
            double distanciaDobra35 = UnitUtils.ConvertToInternalUnits(35, UnitTypeId.Centimeters);
            XYZ pontoDobra = ponta + direcao * distanciaDobra35;

            foreach (var parede in paredesIntersectantes)
            {
                BoundingBoxXYZ bb = parede.get_BoundingBox(null);
                if (bb == null)
                    continue;

                Outline outline = new Outline(bb.Min, bb.Max);
                if (outline.Contains(pontoDobra, 1e-6))
                    return true;
            }

            return false;
        }

        private bool AlgumBlocoNaCotaDeCinta(List<FamilyInstance> blocos, HashSet<double> cotasCintaZ, double toleranciaZ)
        {
            foreach (var fi in blocos)
            {
                if (fi.Location is not LocationPoint locationPoint)
                    continue;

                double chaveArredondada = Math.Round(locationPoint.Point.Z / toleranciaZ, MidpointRounding.AwayFromZero) * toleranciaZ;
                if (cotasCintaZ.Contains(chaveArredondada))
                    return true;
            }

            return false;
        }
    }

    public class ElementSelectionFilter : ISelectionFilter
    {
        private readonly Func<Element, bool> _validateElement;
        private readonly Func<Reference, bool>? _validateReference;

        public ElementSelectionFilter(Func<Element, bool> validateElement)
        {
            _validateElement = validateElement;
        }

        public ElementSelectionFilter(Func<Element, bool> validateElement, Func<Reference, bool> validateReference) : this(validateElement)
        {
            _validateReference = validateReference;
        }

        public bool AllowElement(Element elem)
        {
            return _validateElement(elem);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            if (_validateReference == null) return true;
            return _validateReference.Invoke(reference);
        }
    }

    public class Parede
    {
        private readonly ElementId _id;
        private readonly Wall _wallElement;
        private readonly Document _doc;
        private readonly BuiltInCategory _category;

        // Lazy — caros
        private readonly Lazy<IEnumerable<Solid>> _solids;
        private readonly Lazy<List<FamilyInstance>> _familyInstances;
        private readonly Lazy<List<Bloco>> _blocos;

        // Props baratas — inicializadas no construtor
        public ElementId Id => _id;
        public List<FamilyInstance> Portas { get; private set; }
        public List<FamilyInstance> Janelas { get; private set; }
        public Wall WallElement => _wallElement;
        public BoundingBoxXYZ WallBoundingBox { get; private set; }
        public Outline WallOutline { get; private set; }
        public XYZ VetorDirecaoParede { get; private set; }

        // Props caras — lazy
        public IEnumerable<Solid> WallGeometrySolid => _solids.Value;
        public List<FamilyInstance> WallBlocosFamilyInstances => _familyInstances.Value;
        public List<Bloco> WallBlocos => _blocos.Value;

        public Parede(Wall wallElement, Document doc,
            BuiltInCategory category = BuiltInCategory.OST_GenericModel)
        {
            _id = wallElement.Id;
            _wallElement = wallElement;
            _doc = doc;
            _category = category;

            // Baratos — executa já
            InitializeBoundingBox();
            InitializeDirecao();

            // Caros — registra a receita, executa só quando acessar
            _solids = new Lazy<IEnumerable<Solid>>(
                () => _wallElement.get_Geometry(new Options()).Cast<Solid>()
            );
            _familyInstances = new Lazy<List<FamilyInstance>>(LoadFamilyInstances);
            _blocos = new Lazy<List<Bloco>>(
                () => WallBlocosFamilyInstances.Select(b => new Bloco(b)).ToList()
            );

            // Pega portas e janelas hospedadas na parede
            Portas = InitializePortasOuJanelas(BuiltInCategory.OST_Doors);
            Janelas = InitializePortasOuJanelas(BuiltInCategory.OST_Windows);
        }

        private void InitializeBoundingBox()
        {
            WallBoundingBox = _wallElement.get_BoundingBox(null);
            WallOutline = new Outline(WallBoundingBox.Min, WallBoundingBox.Max);
        }

        private void InitializeDirecao()
        {
            var curve = ((LocationCurve)_wallElement.Location).Curve;
            var inicio = curve.GetEndPoint(0);
            var fim = curve.GetEndPoint(1);
            VetorDirecaoParede = fim.Subtract(inicio).Normalize();
        }

        private List<FamilyInstance> LoadFamilyInstances()
        {
            Solid wallSolid = ObterSolido(_wallElement);

            if (wallSolid == null)
            {
                TaskDialog.Show("Diagnóstico", "wallSolid é null — ObterSolido não encontrou sólido na parede.");
                return new List<FamilyInstance>();
            }

            return new FilteredElementCollector(_doc)
                .OfCategory(_category)
                .WhereElementIsNotElementType()
                .WherePasses(new BoundingBoxIntersectsFilter(WallOutline))
                .WherePasses(new ElementParameterFilter(
                    new FilterStringRule(
                        new ParameterValueProvider(new ElementId(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS)),
                        new FilterStringEquals(), "Bloco")))
                .Cast<FamilyInstance>()
                .Where(fi =>
                {
                    XYZ blocoHandVector = fi.HandOrientation.Normalize();
                    XYZ crossProduct = blocoHandVector.CrossProduct(VetorDirecaoParede);
                    return crossProduct.IsAlmostEqualTo(XYZ.Zero);
                })
                .Where(fi =>
                {
                    if (fi.Location is not LocationPoint locationPoint)
                    {
                        return false;
                    }

                    else
                    {
                        try
                        {
                            return PontoEstaDentroDoSolido(locationPoint.Point, wallSolid);
                        }
                        catch (Exception e)
                        {
                            TaskDialog.Show("Error", e.Message);
                            return false;
                        }
                    }

                })
                .ToList();
        }

        private Solid ObterSolido(Element elemento)
        {
            Options geomOptions = new Options
            {
                ComputeReferences = false,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = true
            };

            GeometryElement geomElement = elemento.get_Geometry(geomOptions);

            if (geomElement == null) return null;

            foreach (GeometryObject geomObj in geomElement)
            {
                if (geomObj is Solid solid && solid.Volume > 0)
                    return solid;

                if (geomObj is GeometryInstance geomInstance)
                {
                    foreach (GeometryObject instanceObj in geomInstance.GetInstanceGeometry())
                    {
                        if (instanceObj is Solid instanceSolid && instanceSolid.Volume > 0)
                            return instanceSolid;
                    }
                }
            }

            return null;
        }

        private bool PontoEstaDentroDoSolido(XYZ ponto, Solid solid)
        {
            try
            {
                double tamanho = 0.05;

                CurveLoop perfil = new CurveLoop();
                perfil.Append(Line.CreateBound(
                    new XYZ(ponto.X - tamanho, ponto.Y - tamanho, ponto.Z),
                    new XYZ(ponto.X + tamanho, ponto.Y - tamanho, ponto.Z)));
                perfil.Append(Line.CreateBound(
                    new XYZ(ponto.X + tamanho, ponto.Y - tamanho, ponto.Z),
                    new XYZ(ponto.X + tamanho, ponto.Y + tamanho, ponto.Z)));
                perfil.Append(Line.CreateBound(
                    new XYZ(ponto.X + tamanho, ponto.Y + tamanho, ponto.Z),
                    new XYZ(ponto.X - tamanho, ponto.Y + tamanho, ponto.Z)));
                perfil.Append(Line.CreateBound(
                    new XYZ(ponto.X - tamanho, ponto.Y + tamanho, ponto.Z),
                    new XYZ(ponto.X - tamanho, ponto.Y - tamanho, ponto.Z)));

                Solid cubo = GeometryCreationUtilities.CreateExtrusionGeometry(
                    new List<CurveLoop> { perfil },
                    XYZ.BasisZ,
                    tamanho * 2);

                Solid intersecao = BooleanOperationsUtils.ExecuteBooleanOperation(
                    solid,
                    cubo,
                    BooleanOperationsType.Intersect);

                return intersecao != null && intersecao.Volume > 0;
            }
            catch
            {
                return false;
            }
        }

        private List<FamilyInstance> InitializePortasOuJanelas(BuiltInCategory category)
        {
            return new FilteredElementCollector(_doc)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(fi => fi.Host != null && fi.Host.Id == _id)
                .ToList();
        }
    }

    public class BlocoSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem.Name.Contains("TBL");
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }

    public class Porta
    {
        public FamilyInstance PortaFamilyInstance { get; set; }
        public double Altura { get; set; }
        public double Espessura { get; set; }
        public double Comprimento { get; set; }
        public double Peitoril { get; set; }
        public Transform PortaTransform { get; set; }
        public Solid SolidPorta { get; set; }

        public Porta(FamilyInstance portaFamilyInstance)
        {
            PortaFamilyInstance = portaFamilyInstance;
        }

        public void AtualizarDimensoes(double espessura)
        {
            if (PortaFamilyInstance != null)
            {
                Altura = PortaFamilyInstance.Symbol.get_Parameter(BuiltInParameter.DOOR_HEIGHT).AsDouble();
                Comprimento = PortaFamilyInstance.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH).AsDouble();
                Espessura = UnitUtils.ConvertToInternalUnits(espessura, UnitTypeId.Centimeters);
                Peitoril = PortaFamilyInstance.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).AsDouble();
                PortaTransform = PortaFamilyInstance.GetTransform();
            }
        }

        public void AtualizarSolid()
        {
            SolidPorta = SolidExtractor.ExtrairSolido(PortaFamilyInstance, PortaTransform, Altura, Comprimento, Espessura);
        }
    }

    public class Janela
    {
        public FamilyInstance JanelaFamilyInstance { get; set; }
        public double Altura { get; set; }
        public double Espessura { get; set; }
        public double Comprimento { get; set; }
        public double Peitoril { get; set; }
        public Transform JanelaTransform { get; set; }

        public Solid JanelaSolid { get; set; }

        public Janela(FamilyInstance janelaFamilyInstance)
        {
            JanelaFamilyInstance = janelaFamilyInstance;
        }

        public void AtualizarDimensoes(double espessura)
        {
            if (JanelaFamilyInstance != null)
            {
                Altura = JanelaFamilyInstance.Symbol.get_Parameter(BuiltInParameter.WINDOW_HEIGHT).AsDouble();
                Comprimento = JanelaFamilyInstance.Symbol.get_Parameter(BuiltInParameter.WINDOW_WIDTH).AsDouble();
                Espessura = UnitUtils.ConvertToInternalUnits(espessura, UnitTypeId.Centimeters);
                Peitoril = JanelaFamilyInstance.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).AsDouble();
                JanelaTransform = JanelaFamilyInstance.GetTransform();
            }
        }

        public void AtualizarSolid()
        {
            JanelaSolid = SolidExtractor.ExtrairSolido(JanelaFamilyInstance, JanelaTransform, Altura, Comprimento, Espessura);
        }
    }

    public class Bloco
    {
        public Autodesk.Revit.DB.FamilyInstance BlocoFamilyInstance { get; set; }
        public Autodesk.Revit.DB.Transform BlocoTransform { get; set; }
        public double BlocoLargura { get; set; }
        public double BlocoComprimento { get; set; }
        public double BlocoAltura { get; set; }
        public Septo? Septo1 { get; set; } //positivo em relação ao Hand
        public Septo? Septo2 { get; set; } //negativo em relação ao Hand
        public Septo? Septo3 { get; set; } //no centro do bloco
        public Bloco(Autodesk.Revit.DB.FamilyInstance blocoFamilyInstance)
        {
            BlocoFamilyInstance = blocoFamilyInstance;
            BlocoTransform = blocoFamilyInstance.GetTransform();
            BlocoLargura = UnitUtils.ConvertFromInternalUnits(blocoFamilyInstance.Symbol.LookupParameter("Largura").AsDouble(), UnitTypeId.Centimeters);
            BlocoComprimento = UnitUtils.ConvertFromInternalUnits(blocoFamilyInstance.Symbol.LookupParameter("Comprimento").AsDouble(), UnitTypeId.Centimeters);
            BlocoAltura = UnitUtils.ConvertFromInternalUnits(blocoFamilyInstance.LookupParameter("SP_H_BLOCO").AsDouble(), UnitTypeId.Centimeters);
        }

        public void CriarSeptosTemporarios(Document doc, FamilySymbol? detailSymbol)
        {
            XYZ origin = BlocoTransform.Origin;  // ponto de origem
            XYZ basisX = BlocoTransform.BasisX;  // eixo X local (direção "hand")
            XYZ basisY = BlocoTransform.BasisY;  // eixo Y local (direção "facing")
            XYZ basisZ = BlocoTransform.BasisZ;  // eixo Z local (normal ao plano)

            double offsetPositiveXValor = 10;
            double offsetNegativeXValor = 10;

            if (BlocoComprimento == 34)
            {
                offsetPositiveXValor = 7.5;
            }

            else if (BlocoComprimento == 54)
            {
                offsetPositiveXValor = 17.5;
                offsetNegativeXValor = 17.5;
            }

            double offsetPositiveX = UnitUtils.ConvertToInternalUnits(offsetPositiveXValor, UnitTypeId.Centimeters);
            double offsetNegativeX = UnitUtils.ConvertToInternalUnits(offsetNegativeXValor, UnitTypeId.Centimeters);
            XYZ point1 = origin + basisX * offsetPositiveX;
            XYZ point2 = origin - basisX * offsetNegativeX;
            XYZ point3 = origin;

            if (detailSymbol == null)
                throw new Exception("Nenhum FamilySymbol de item de detalhe encontrado.");

            // Obter a view ativa
            View activeView = doc.ActiveView;

            // Calcular o ângulo de rotação a partir do vetor Hand
            // O ângulo é em relação ao eixo X global (1, 0, 0)
            XYZ xAxis = XYZ.BasisX;
            double angle = Math.Atan2(basisX.Y, basisX.X);

            #region Transaction para criar septos temporários
            if (BlocoComprimento > 9)
            {
                using (Transaction tx1 = new Transaction(doc, "Criar itens de detalhe"))
                {
                    tx1.Start();

                    // Desativa regeneração automática durante o loop
                    tx1.SetFailureHandlingOptions(
                        tx1.GetFailureHandlingOptions()
                         .SetDelayedMiniWarnings(true));

                    // Ativar o symbol se necessário
                    if (!detailSymbol.IsActive)
                        detailSymbol.Activate();

                    // Criar instância 1
                    FamilyInstance detail1 = doc.Create.NewFamilyInstance(
                        point1, detailSymbol, activeView);

                    // Criar instância 2
                    FamilyInstance detail2 = doc.Create.NewFamilyInstance(
                        point2, detailSymbol, activeView);

                    // Criar instância 3 (no centro do bloco)
                    FamilyInstance detail3 = doc.Create.NewFamilyInstance(
                        point3, detailSymbol, activeView);

                    // Aplicar rotação para alinhar com o Hand da família original
                    // Rotacionar em torno do eixo Z passando pelo ponto de inserção
                    if (Math.Abs(angle) > 1e-9)
                    {
                        Line axis1 = Line.CreateBound(point1, point1 + XYZ.BasisZ);
                        Line axis2 = Line.CreateBound(point2, point2 + XYZ.BasisZ);
                        Line axis3 = Line.CreateBound(point3, point3 + XYZ.BasisZ);
                        ElementTransformUtils.RotateElement(doc, detail1.Id, axis1, angle);
                        ElementTransformUtils.RotateElement(doc, detail2.Id, axis2, angle);
                        ElementTransformUtils.RotateElement(doc, detail3.Id, axis3, angle);
                    }

                    // Linhas de projeção e corte em vermelho
                    OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                    Color red = new Color(255, 0, 0);

                    ogs.SetProjectionLineColor(red);
                    ogs.SetCutLineColor(red);

                    // Espessura de linha (valor entre 1 e 16, conforme tabela de pesos do Revit)
                    ogs.SetProjectionLineWeight(5); // linhas de projeção
                    ogs.SetCutLineWeight(5);        // linhas de corte

                    activeView.SetElementOverrides(detail1.Id, ogs);
                    activeView.SetElementOverrides(detail2.Id, ogs);
                    activeView.SetElementOverrides(detail3.Id, ogs);

                    // Preenchimento em vermelho (se aplicável)
                    FillPatternElement solidFill = new FilteredElementCollector(doc)
                        .OfClass(typeof(FillPatternElement))
                        .Cast<FillPatternElement>()
                        .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);

                    if (solidFill != null)
                    {
                        ogs.SetSurfaceForegroundPatternId(solidFill.Id);
                        ogs.SetSurfaceForegroundPatternColor(red);
                        ogs.SetCutForegroundPatternId(solidFill.Id);
                        ogs.SetCutForegroundPatternColor(red);
                    }

                    activeView.SetElementOverrides(detail1.Id, ogs);
                    activeView.SetElementOverrides(detail2.Id, ogs);
                    activeView.SetElementOverrides(detail3.Id, ogs);

                    if (BlocoComprimento == 19 && BlocoLargura == 14)
                    {
                        doc.Delete(detail1.Id);
                        doc.Delete(detail2.Id);
                    }

                    else if (BlocoComprimento == 19 && BlocoLargura == 19)
                    {
                        doc.Delete(detail1.Id);
                        doc.Delete(detail2.Id);
                        detail3.LookupParameter("TIPO").Set(4);
                    }

                    else if (BlocoComprimento == 34)
                    {
                        doc.Delete(detail3.Id);
                        detail2.LookupParameter("TIPO").Set(2);
                    }

                    else if (BlocoComprimento == 39 && BlocoLargura == 14)
                    {
                        doc.Delete(detail3.Id);
                    }

                    else if (BlocoComprimento == 39 && BlocoLargura == 19)
                    {
                        doc.Delete(detail3.Id);
                        detail1.LookupParameter("TIPO").Set(4);
                        detail2.LookupParameter("TIPO").Set(4);
                    }

                    else if (BlocoComprimento == 54)
                    {
                        detail3.LookupParameter("TIPO").Set(2);
                    }

                    tx1.Commit();
                }
            }
            #endregion
        }

        public Solid CriarSolidoBloco()
        {
            XYZ origin = BlocoTransform.Origin;
            XYZ basisX = BlocoTransform.BasisX;
            XYZ basisY = BlocoTransform.BasisY;
            XYZ basisZ = BlocoTransform.BasisZ;

            var largSobre2 = UnitUtils.ConvertToInternalUnits(BlocoLargura / 2, UnitTypeId.Centimeters);
            var compSobre2 = UnitUtils.ConvertToInternalUnits(BlocoComprimento / 2, UnitTypeId.Centimeters);

            XYZ ponto1 = new XYZ(-compSobre2, -largSobre2, 0);
            XYZ ponto2 = new XYZ(compSobre2, -largSobre2, 0);
            XYZ ponto3 = new XYZ(compSobre2, largSobre2, 0);
            XYZ ponto4 = new XYZ(-compSobre2, largSobre2, 0);

            CurveLoop perfil = new CurveLoop();
            perfil.Append(Line.CreateBound(ponto1, ponto2));
            perfil.Append(Line.CreateBound(ponto2, ponto3));
            perfil.Append(Line.CreateBound(ponto3, ponto4));
            perfil.Append(Line.CreateBound(ponto4, ponto1));

            Solid solidOrigem = GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { perfil },
                XYZ.BasisZ, UnitUtils.ConvertToInternalUnits(BlocoAltura, UnitTypeId.Centimeters));

            Solid solidPosicionado;
            try
            {
                LocationPoint locationPoint = BlocoFamilyInstance.Location as LocationPoint;
                XYZ posicao = locationPoint.Point;
                double rotacao = locationPoint.Rotation;

                Transform tRotacao = Transform.CreateRotation(XYZ.BasisZ, rotacao);
                Transform tTranslacao = Transform.CreateTranslation(new XYZ(posicao.X, posicao.Y, posicao.Z + UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Centimeters)));
                Transform transform = tTranslacao.Multiply(tRotacao);

                solidPosicionado = SolidUtils.CreateTransformed(solidOrigem, transform);
            }
            finally
            {
                solidOrigem.Dispose();
            }

            return solidPosicionado;
        }

    }

    public static class SolidExtractor
    {
        public static Solid ExtrairSolido(FamilyInstance familyInstance, Transform elementTransform, double altura, double comprimento, double largura)
        {
            XYZ origin = elementTransform.Origin;
            XYZ basisX = elementTransform.BasisX;
            XYZ basisY = elementTransform.BasisY;
            XYZ basisZ = elementTransform.BasisZ;

            var largSobre2 = largura / 2;
            var compSobre2 = comprimento / 2;

            XYZ ponto1 = new XYZ(-compSobre2, -largSobre2, 0);
            XYZ ponto2 = new XYZ(compSobre2, -largSobre2, 0);
            XYZ ponto3 = new XYZ(compSobre2, largSobre2, 0);
            XYZ ponto4 = new XYZ(-compSobre2, largSobre2, 0);

            CurveLoop perfil = new CurveLoop();
            perfil.Append(Line.CreateBound(ponto1, ponto2));
            perfil.Append(Line.CreateBound(ponto2, ponto3));
            perfil.Append(Line.CreateBound(ponto3, ponto4));
            perfil.Append(Line.CreateBound(ponto4, ponto1));

            Solid solidOrigem = GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { perfil },
                XYZ.BasisZ, altura);

            try
            {
                LocationPoint locationPoint = familyInstance.Location as LocationPoint;
                XYZ posicao = locationPoint.Point;
                double rotacao = locationPoint.Rotation;

                Transform tRotacao = Transform.CreateRotation(XYZ.BasisZ, rotacao);
                Transform tTranslacao = Transform.CreateTranslation(new XYZ(posicao.X, posicao.Y, posicao.Z));
                Transform transform = tTranslacao.Multiply(tRotacao);

                return SolidUtils.CreateTransformed(solidOrigem, transform);
            }
            finally
            {
                solidOrigem.Dispose();
            }
        }
    }

    public class Septo
    {
        public Autodesk.Revit.DB.FamilyInstance SeptoFamilyInstance { get; set; }
        public int Tipo { get; set; }

        public Transform SeptoTransform { get; set; }
        public XYZ SeptoOrigin { get; set; }

        public Septo(FamilyInstance familyInstance)
        {
            SeptoFamilyInstance = familyInstance;
            Tipo = familyInstance.LookupParameter("TIPO")?.AsInteger() ?? 1;
            SeptoTransform = familyInstance.GetTransform();
            SeptoOrigin = SeptoTransform.Origin;
        }
    }
}