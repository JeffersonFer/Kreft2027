using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace KreftBimTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    internal class CompatibilizadorBlocos : IExternalCommand
    {
        private Dictionary<string, FamilySymbol> _familySymbols = new Dictionary<string, FamilySymbol>();
        private List<string> _familyNamesBL14 = new List<string> { "TBL4X19", "TBL9X19", "TBL14X19", "TBL14X34", "TBL14X39", "TBL14X54" };
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var app = uiApp.Application;
            var uidoc = uiApp.ActiveUIDocument;
            var doc = uidoc.Document;

            _familySymbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .Cast<FamilySymbol>()
                .Where(fs => _familyNamesBL14.Contains(fs.Name))
                .ToDictionary(fs => fs.Name, fs => fs);

            TaskDialog compatibilizarBlocos = new TaskDialog("Compatibilizador de Blocos")
            {
                MainInstruction = "Compatibilizador de Blocos - DESEJA PROSSEGUIR?",
                MainContent = "Selecione as paredes. \n" +
                    "O comando irá verificar as aberturas presentes nas paredes e compatibilizar com os blocos.\n\n" +
                    "Para muitas paredes selecionadas, o Revit pode aparentar estar 'Não respondendo' durante o processamento — isso é esperado, aguarde a conclusão sem fechar o programa.",
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                DefaultButton = TaskDialogResult.No
            };

            if (compatibilizarBlocos.Show() != TaskDialogResult.Yes)
            {
                TaskDialog.Show("Cancelado", "Operação Cancelada");
                return Result.Cancelled;
            }

            try
            {
                //Seleção de paredes
                List<Parede> paredes;
                try
                {
                    TaskDialog.Show("Procedimento", "Selecione as paredes.");
                    ElementSelectionFilter wallFilter = new ElementSelectionFilter(e => e is Wall);
                    paredes = uidoc.Selection.PickObjects(ObjectType.Element, wallFilter, "Selecione as paredes")
                        .Select(w => new Parede((Wall)doc.GetElement(w), doc)).ToList();
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    TaskDialog.Show("Cancelado", "Operação Cancelada");
                    return Result.Cancelled;
                }

                //Lista de erros
                List<string> errosProcessamento = new List<string>();

                using (TransactionGroup transactionGroup = new TransactionGroup(doc, "Compatibilizar Blocos"))
                {
                    transactionGroup.Start();

                    using (Transaction tx1 = new Transaction(doc, "Ativar Family Symbols"))
                    {
                        tx1.Start();

                        // Desativa regeneração automática durante o loop
                        tx1.SetFailureHandlingOptions(
                            tx1.GetFailureHandlingOptions()
                             .SetDelayedMiniWarnings(true));

                        foreach (FamilySymbol familySymbol in _familySymbols.Values)
                        {
                            if (!familySymbol.IsActive)
                            {
                                familySymbol.Activate();
                            }
                        }

                        tx1.Commit();
                    }

                    foreach (var parede in paredes)
                    {
                        try
                        {
                            var portas = parede.Portas.Select(p => new Porta(p)).ToList();
                            var janelas = parede.Janelas.Select(j => new Janela(j)).ToList();

                            foreach (var janela in janelas)
                            {
                                try
                                {
                                    janela.AtualizarDimensoes(14);
                                    janela.AtualizarSolid();
                                    XYZ janelOrigem = janela.JanelaFamilyInstance.GetTransform().Origin;

                                    var janelaBoundingBox = janela.JanelaFamilyInstance.get_BoundingBox(null);
                                    Outline janelaOutline = new Outline(janelaBoundingBox.Min, janelaBoundingBox.Max);
                                    var blocosIntersectados = new FilteredElementCollector(doc)
                                        .OfCategory(BuiltInCategory.OST_GenericModel)
                                        .WhereElementIsNotElementType()
                                        .WherePasses(new BoundingBoxIntersectsFilter(janelaOutline))
                                        .WherePasses(new ElementParameterFilter(
                                            new FilterStringRule(
                                            new ParameterValueProvider(new ElementId(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS)),
                                            new FilterStringEquals(), "Bloco")))
                                        .Cast<FamilyInstance>().ToList();

                                    List<Bloco> blocos = blocosIntersectados.Select(b => new Bloco(b)).ToList();

                                    using (Transaction tx2 = new Transaction(doc, "Compatibilizar Janela"))
                                    {
                                        tx2.Start();
                                        tx2.SetFailureHandlingOptions(
                                            tx2.GetFailureHandlingOptions().SetDelayedMiniWarnings(true));

                                        foreach (var bloco in blocos)
                                        {
                                            try
                                            {
                                                Solid solidoBloco = null;
                                                Solid resultadoBooleanoBlocoJanela = null;
                                                try
                                                {
                                                    solidoBloco = bloco.CriarSolidoBloco();

                                                    resultadoBooleanoBlocoJanela = BooleanOperationsUtils.ExecuteBooleanOperation(
                                                        solidoBloco, janela.JanelaSolid, BooleanOperationsType.Difference);

                                                    double resultadoVolume = resultadoBooleanoBlocoJanela.Volume;

                                                    using (SubTransaction subTx = new SubTransaction(doc))
                                                    {
                                                        subTx.Start();
                                                        try
                                                        {
                                                            if (resultadoVolume == 0)
                                                            {
                                                                doc.Delete(bloco.BlocoFamilyInstance.Id);
                                                            }
                                                            else if (resultadoVolume > 0 && resultadoVolume < solidoBloco.Volume)
                                                            {
                                                                XYZ resultadoCentroide = resultadoBooleanoBlocoJanela.ComputeCentroid();
                                                                XYZ vetorSolidAbertura = (new XYZ(janelOrigem.X, janelOrigem.Y, resultadoCentroide.Z) - resultadoCentroide).Normalize();

                                                                InstanciarBlocoNoCentroDoSolido(bloco, doc, _familySymbols, resultadoVolume, resultadoCentroide, vetorSolidAbertura);
                                                                doc.Delete(bloco.BlocoFamilyInstance.Id);
                                                            }
                                                            subTx.Commit();
                                                        }
                                                        catch
                                                        {
                                                            subTx.RollBack();
                                                            throw;
                                                        }
                                                    }
                                                }
                                                finally
                                                {
                                                    solidoBloco?.Dispose();
                                                    resultadoBooleanoBlocoJanela?.Dispose();
                                                }
                                            }
                                            catch (Exception exBloco)
                                            {
                                                errosProcessamento.Add($"Id: {bloco.BlocoFamilyInstance.Id} - Erro ao processar bloco: {exBloco.Message}");
                                            }
                                        }

                                        tx2.Commit();
                                    }

                                    janela.JanelaSolid?.Dispose();
                                }
                                catch (Exception exJanela)
                                {
                                    errosProcessamento.Add($"Id: {janela.JanelaFamilyInstance.Id} - Erro ao processar janela: {exJanela.Message}");
                                }
                            }

                            foreach (var porta in portas)
                            {
                                try
                                {
                                    porta.AtualizarDimensoes(14);
                                    porta.AtualizarSolid();
                                    XYZ portaOrigem = porta.PortaFamilyInstance.GetTransform().Origin;

                                    var portaBoundingBox = porta.PortaFamilyInstance.get_BoundingBox(null);
                                    Outline portaOutline = new Outline(portaBoundingBox.Min, portaBoundingBox.Max);
                                    var blocosIntersectados = new FilteredElementCollector(doc)
                                        .OfCategory(BuiltInCategory.OST_GenericModel)
                                        .WhereElementIsNotElementType()
                                        .WherePasses(new BoundingBoxIntersectsFilter(portaOutline))
                                        .WherePasses(new ElementParameterFilter(
                                            new FilterStringRule(
                                            new ParameterValueProvider(new ElementId(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS)),
                                            new FilterStringEquals(), "Bloco")))
                                        .Cast<FamilyInstance>().ToList();

                                    List<Bloco> blocos = blocosIntersectados.Select(b => new Bloco(b)).ToList();

                                    using (Transaction tx2 = new Transaction(doc, "Compatibilizar Porta"))
                                    {
                                        tx2.Start();
                                        tx2.SetFailureHandlingOptions(
                                            tx2.GetFailureHandlingOptions().SetDelayedMiniWarnings(true));

                                        foreach (var bloco in blocos)
                                        {
                                            try
                                            {
                                                Solid solidoBloco = null;
                                                Solid resultadoBooleanoBlocoPorta = null;
                                                try
                                                {
                                                    solidoBloco = bloco.CriarSolidoBloco();

                                                    resultadoBooleanoBlocoPorta = BooleanOperationsUtils.ExecuteBooleanOperation(
                                                        solidoBloco, porta.SolidPorta, BooleanOperationsType.Difference);

                                                    double resultadoVolume = resultadoBooleanoBlocoPorta.Volume;

                                                    using (SubTransaction subTx = new SubTransaction(doc))
                                                    {
                                                        subTx.Start();
                                                        try
                                                        {
                                                            if (resultadoVolume == 0)
                                                            {
                                                                doc.Delete(bloco.BlocoFamilyInstance.Id);
                                                            }
                                                            else if (resultadoVolume > 0 && resultadoVolume < solidoBloco.Volume)
                                                            {
                                                                XYZ resultadoCentroide = resultadoBooleanoBlocoPorta.ComputeCentroid();
                                                                XYZ vetorSolidAbertura = (new XYZ(portaOrigem.X, portaOrigem.Y, resultadoCentroide.Z) - resultadoCentroide).Normalize();

                                                                InstanciarBlocoNoCentroDoSolido(bloco, doc, _familySymbols, resultadoVolume, resultadoCentroide, vetorSolidAbertura);
                                                                doc.Delete(bloco.BlocoFamilyInstance.Id);
                                                            }
                                                            subTx.Commit();
                                                        }
                                                        catch
                                                        {
                                                            subTx.RollBack();
                                                            throw;
                                                        }
                                                    }
                                                }
                                                finally
                                                {
                                                    solidoBloco?.Dispose();
                                                    resultadoBooleanoBlocoPorta?.Dispose();
                                                }
                                            }
                                            catch (Exception exBloco)
                                            {
                                                errosProcessamento.Add($"Id: {bloco.BlocoFamilyInstance.Id} - Erro ao processar bloco: {exBloco.Message}");
                                            }
                                        }

                                        tx2.Commit();
                                    }

                                    porta.SolidPorta?.Dispose();
                                }
                                catch (Exception exPorta)
                                {
                                    errosProcessamento.Add($"Id: {porta.PortaFamilyInstance.Id} - Erro ao processar porta: {exPorta.Message}");
                                }
                            }


                        }
                        catch (Exception exParede)
                        {
                            errosProcessamento.Add($"Id: {parede.Id} - Erro ao processar parede: {exParede.Message}\"");
                        }
                    }

                    transactionGroup.Assimilate();

                    if (errosProcessamento.Count > 0)
                    {
                        TaskDialog resultadoDialog = new TaskDialog("Compatibilizador de Blocos - Concluído")
                        {
                            MainInstruction = "Processamento concluído com erros",
                            MainContent = $"{errosProcessamento.Count} elemento(s) não foram processados corretamente.",
                            ExpandedContent = string.Join(Environment.NewLine, errosProcessamento)
                        };
                        resultadoDialog.Show();
                    }
                    else
                    {
                        TaskDialog.Show("Compatibilizador de Blocos - Concluído", "Processamento concluído sem erros.");
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Erro", $"Ocorreu um erro: {ex.Message}");
                return Result.Failed;
            }


            return Result.Succeeded;

        }

        public void InstanciarBlocoNoCentroDoSolido(Bloco bloco,
            Document doc, Dictionary<string, FamilySymbol> familySymbols,
            double volume, XYZ centroide, XYZ vetorSolidAbertura, double espessuraBloco = 14, double alturaBloco = 19)
        {
            //Cálculo do espaçamento disponível para determinar o bloco a ser instanciado
            var espacoDisponivel = (volume / UnitUtils.ConvertToInternalUnits(espessuraBloco,
                UnitTypeId.Centimeters)) / UnitUtils.ConvertToInternalUnits(alturaBloco, UnitTypeId.Centimeters);

            FamilySymbol? blocoDefinido1 = null;
            FamilySymbol? blocoDefinido2 = null;
            int caso = 1;
            double deslocamentoParaAbertura = 0;
            double deslocamentoBloco1 = 0;
            double deslocamentoBloco2 = 0;

            if (espacoDisponivel >= UnitUtils.ConvertToInternalUnits(52, UnitTypeId.Centimeters))
            {
                blocoDefinido1 = familySymbols["TBL14X54"];
                deslocamentoParaAbertura = DeslocarParaAbertura(espacoDisponivel, 54);
            }
            else if (espacoDisponivel >= UnitUtils.ConvertToInternalUnits(37, UnitTypeId.Centimeters))
            {
                blocoDefinido1 = familySymbols["TBL14X39"];
                deslocamentoParaAbertura = DeslocarParaAbertura(espacoDisponivel, 39);
            }
            else if (espacoDisponivel >= UnitUtils.ConvertToInternalUnits(32, UnitTypeId.Centimeters))
            {
                blocoDefinido1 = familySymbols["TBL14X34"];
                deslocamentoParaAbertura = DeslocarParaAbertura(espacoDisponivel, 34);
            }
            else if (espacoDisponivel >= UnitUtils.ConvertToInternalUnits(28, UnitTypeId.Centimeters))
            {
                blocoDefinido1 = familySymbols["TBL14X19"];
                blocoDefinido2 = familySymbols["TBL9X19"];
                deslocamentoParaAbertura = DeslocarParaAbertura(espacoDisponivel, 28);
                deslocamentoBloco1 = UnitUtils.ConvertToInternalUnits(4.5, UnitTypeId.Centimeters);
                deslocamentoBloco2 = UnitUtils.ConvertToInternalUnits(9.5, UnitTypeId.Centimeters);
                caso = 2;
            }
            else if (espacoDisponivel >= UnitUtils.ConvertToInternalUnits(23, UnitTypeId.Centimeters))
            {
                blocoDefinido1 = familySymbols["TBL14X19"];
                blocoDefinido2 = familySymbols["TBL4X19"];
                deslocamentoParaAbertura = DeslocarParaAbertura(espacoDisponivel, 23);
                deslocamentoBloco1 = UnitUtils.ConvertToInternalUnits(2, UnitTypeId.Centimeters);
                deslocamentoBloco2 = UnitUtils.ConvertToInternalUnits(9.5, UnitTypeId.Centimeters);
                caso = 2;
            }
            else if (espacoDisponivel >= UnitUtils.ConvertToInternalUnits(17, UnitTypeId.Centimeters))
            {
                blocoDefinido1 = familySymbols["TBL14X19"];
                deslocamentoParaAbertura = DeslocarParaAbertura(espacoDisponivel, 19);
            }
            else if (espacoDisponivel >= UnitUtils.ConvertToInternalUnits(13, UnitTypeId.Centimeters))
            {
                blocoDefinido1 = familySymbols["TBL9X19"];
                blocoDefinido2 = familySymbols["TBL4X19"];
                deslocamentoParaAbertura = DeslocarParaAbertura(espacoDisponivel, 13);
                deslocamentoBloco1 = UnitUtils.ConvertToInternalUnits(2, UnitTypeId.Centimeters);
                deslocamentoBloco2 = UnitUtils.ConvertToInternalUnits(4.5, UnitTypeId.Centimeters);
                caso = 2;
            }
            else if (espacoDisponivel >= UnitUtils.ConvertToInternalUnits(7, UnitTypeId.Centimeters))
            {
                blocoDefinido1 = familySymbols["TBL9X19"];
                deslocamentoParaAbertura = DeslocarParaAbertura(espacoDisponivel, 9);
            }
            else if (espacoDisponivel >= UnitUtils.ConvertToInternalUnits(3, UnitTypeId.Centimeters))
            {
                blocoDefinido1 = familySymbols["TBL4X19"];
                deslocamentoParaAbertura = DeslocarParaAbertura(espacoDisponivel, 4);
            }
            else
            {
                //Do nothing, não há espaço suficiente para instanciar um bloco
            }

            double deslocamentoZ = UnitUtils.ConvertToInternalUnits(10.5, UnitTypeId.Centimeters);

            // Obter transform do bloco
            var blocoTransform = bloco.BlocoTransform;

            // Obter a orientação do bloco
            XYZ blocoBasisX = blocoTransform.BasisX;

            // Calcular a orientação do novo bloco com base na orientação do bloco original
            double angle = Math.Atan2(blocoBasisX.Y, blocoBasisX.X);

            if (caso == 1)
            {
                //Criar novo bloco na posição caso 1;
                XYZ pointInstance = new XYZ(centroide.X, centroide.Y, centroide.Z - deslocamentoZ);
                FamilyInstance novoBloco = doc.Create.NewFamilyInstance(pointInstance, blocoDefinido1, StructuralType.NonStructural);

                //Aplicar a rotação ao novo bloco para alinhar com o bloco original
                if (Math.Abs(angle) > 1e-9)
                {
                    Line eixo = Line.CreateBound(pointInstance, pointInstance + XYZ.BasisZ);
                    ElementTransformUtils.RotateElement(doc, novoBloco.Id, eixo, angle);
                }
                ElementTransformUtils.MoveElement(doc, novoBloco.Id, deslocamentoParaAbertura * vetorSolidAbertura);
            }

            else if (caso == 2)
            {
                //Criar novo bloco na posição caso 1;
                XYZ pointInstance = new XYZ(centroide.X, centroide.Y, centroide.Z - deslocamentoZ);
                FamilyInstance novoBloco1 = doc.Create.NewFamilyInstance(pointInstance, blocoDefinido1, StructuralType.NonStructural);
                FamilyInstance novoBloco2 = doc.Create.NewFamilyInstance(pointInstance, blocoDefinido2, StructuralType.NonStructural);

                //Aplicar a rotação ao novo bloco para alinhar com o bloco original
                if (Math.Abs(angle) > 1e-9)
                {
                    Line eixo = Line.CreateBound(pointInstance, pointInstance + XYZ.BasisZ);
                    ElementTransformUtils.RotateElement(doc, novoBloco1.Id, eixo, angle);
                    ElementTransformUtils.RotateElement(doc, novoBloco2.Id, eixo, angle);
                }
                ElementTransformUtils.MoveElement(doc, novoBloco1.Id, -1 * (deslocamentoParaAbertura + deslocamentoBloco1) * vetorSolidAbertura);
                ElementTransformUtils.MoveElement(doc, novoBloco2.Id, (deslocamentoParaAbertura + deslocamentoBloco2) * vetorSolidAbertura);
            }

        }

        public double DeslocarParaAbertura(double espacoDisponivel, double compBloco)
        {
            return UnitUtils.ConvertToInternalUnits(
                    (UnitUtils.ConvertFromInternalUnits(espacoDisponivel, UnitTypeId.Centimeters) - compBloco) / 2, UnitTypeId.Centimeters);
        }
    }
}
