using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KreftBimTools.Core.Domain.Corpus;
using KreftBimTools.Revit.Domain;
using KreftBimTools.Revit.Domain.Corpus;
using KreftBimTools.Revit.Domain.SelectionFilters;

namespace KreftBimTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    internal class ColetarCorpusCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;

            List<Reference> paredesSelecionadas;

            try
            {
                paredesSelecionadas = uidoc.Selection
                    .PickObjects(
                        ObjectType.Element,
                        new ParedeEstruturalFilter(),
                        "Selecione uma parede estrutural para teste"
                    ).ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            var resumo = new List<string>();

            foreach (var paredeRef in paredesSelecionadas)
            {
                var wall = doc.GetElement(paredeRef) as Wall;
                var pontos = ObterPontosDeParede(doc, wall);
                var trechos = SegmentadorDeTrechos.Segmentar(pontos);

                var textoTrechos = new List<string>();

                foreach (var trecho in trechos)
                {
                    var trechoDados = MontarTrechoDados(doc, wall, trecho);
                    var trechoInvertido = TrechoDadosInversor.Inverter(trechoDados);

                    textoTrechos.Add(
                        "== ORIGINAL ==\n" + FormatarTrecho(trechoDados) +
                        "\n== INVERTIDO ==\n" + FormatarTrecho(trechoInvertido));
                }

                resumo.Add($"Parede {wall.Id} — {trechos.Count} trecho(s)\n{string.Join("\n\n", textoTrechos)}");
            }

            TaskDialog.Show("Coletor de Corpus", string.Join("\n\n---\n\n", resumo));

            return Result.Succeeded;
        }

        private string FormatarTrecho(TrechoDados t)
        {
            var textoFiada1 = string.Join(", ", t.Fiada1.Select(b => $"{b.Tipo}@{b.PosicaoX:F1}"));
            var textoFiada2 = string.Join(", ", t.Fiada2.Select(b => $"{b.Tipo}@{b.PosicaoX:F1}"));
            var textoAberturas = string.Join(", ", t.Aberturas.Select(a => $"centro={a.PosicaoXCentro:F1},comp={a.Comprimento:F1}"));

            return
                $"  Espessura={t.EspessuraParede:F2} | Comprimento={t.ComprimentoTrecho:F2}\n" +
                $"  InicioLivre={t.InicioEhLivre} | FimLivre={t.FimEhLivre}\n" +
                $"  AmarraçãoInicio F1={t.AmarracaoFiada1NoInicio} F2={t.AmarracaoFiada2NoInicio}\n" +
                $"  AmarraçãoFim    F1={t.AmarracaoFiada1NoFim} F2={t.AmarracaoFiada2NoFim}\n" +
                $"  Fiada1: [{textoFiada1}]\n" +
                $"  Fiada2: [{textoFiada2}]\n" +
                $"  Aberturas: [{textoAberturas}]";
        }

        private List<PontoDeParede> ObterPontosDeParede(Document document, Wall paredeAlvo)
        {
            var curvaAlvo = (paredeAlvo.Location as LocationCurve)?.Curve;
            if (curvaAlvo == null)
                return new List<PontoDeParede>();

            var outrasParedes = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .Where(w => w.Id != paredeAlvo.Id)
                .Where(w => RevitElementoIdentificador.IsParedeEstrutural(w))
                .ToList();

            var pontosIntermediarios = new List<XYZ>();

            foreach (var outraParede in outrasParedes)
            {
                var curvaOutra = (outraParede.Location as LocationCurve)?.Curve;
                if (curvaOutra == null) continue;

#if REVIT2025
                IntersectionResultArray resultados;
                var resultado = curvaAlvo.Intersect(curvaOutra, out resultados);

                if (resultado == SetComparisonResult.Overlap && resultados != null)
                {
                    foreach (IntersectionResult ir in resultados)
                    {
                        pontosIntermediarios.Add(ir.XYZPoint);
                    }
                }
#else
                var resultadoDetalhado = curvaAlvo.Intersect(curvaOutra, CurveIntersectResultOption.Detailed);

                if (resultadoDetalhado != null)
                {
                    foreach (var overlap in resultadoDetalhado.GetOverlaps())
                    {
                        pontosIntermediarios.Add(overlap.Point);
                    }
                }
#endif
            }

            var pontoInicial = curvaAlvo.GetEndPoint(0);
            var pontoFinal = curvaAlvo.GetEndPoint(1);

            var pontosFiltrados = pontosIntermediarios
                .Where(p => !p.IsAlmostEqualTo(pontoInicial) && !p.IsAlmostEqualTo(pontoFinal))
                .OrderBy(p => curvaAlvo.Project(p).Parameter)
                .ToList();

            bool piTemIntersecao = pontosIntermediarios.Any(p => p.IsAlmostEqualTo(pontoInicial));
            bool pfTemIntersecao = pontosIntermediarios.Any(p => p.IsAlmostEqualTo(pontoFinal));

            var resultado_pontos = new List<PontoDeParede>
            {
                new PontoDeParede(
                    pontoInicial,
                    piTemIntersecao ? "PI" : "PIL",
                    VerificarAmarracao(document, paredeAlvo, pontoInicial, 9),
                    VerificarAmarracao(document, paredeAlvo, pontoInicial, 29))
            };

            for (int i = 0; i < pontosFiltrados.Count; i++)
            {
                resultado_pontos.Add(new PontoDeParede(
                    pontosFiltrados[i],
                    $"I{i + 1}",
                    VerificarAmarracao(document, paredeAlvo, pontosFiltrados[i], 9),
                    VerificarAmarracao(document, paredeAlvo, pontosFiltrados[i], 29)));
            }

            resultado_pontos.Add(new PontoDeParede(
                pontoFinal,
                pfTemIntersecao ? "PF" : "PFL",
                VerificarAmarracao(document, paredeAlvo, pontoFinal, 9),
                VerificarAmarracao(document, paredeAlvo, pontoFinal, 29)));

            return resultado_pontos;
        }

        private bool VerificarAmarracao(Document document, Wall parede, XYZ ponto, double alturaZCm)
        {
            XYZ direcaoParede = parede.Orientation;

            double tamanhoMetadeBB = UnitUtils.ConvertToInternalUnits(2, UnitTypeId.Centimeters);
            double alturaConvertida = UnitUtils.ConvertToInternalUnits(alturaZCm, UnitTypeId.Centimeters);

            XYZ centro = new XYZ(ponto.X, ponto.Y, ponto.Z + alturaConvertida);

            Outline outline = new Outline(
                centro - new XYZ(tamanhoMetadeBB, tamanhoMetadeBB, tamanhoMetadeBB),
                centro + new XYZ(tamanhoMetadeBB, tamanhoMetadeBB, tamanhoMetadeBB)
            );

            var candidatos = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .WhereElementIsNotElementType()
                .WherePasses(new BoundingBoxIntersectsFilter(outline))
                .Cast<FamilyInstance>()
                .Where(fi => RevitElementoIdentificador.IsBloco(fi))
                .ToList();

            foreach (var candidato in candidatos)
            {
                XYZ facingBloco = candidato.FacingOrientation.Normalize();
                XYZ crossProduct = facingBloco.CrossProduct(direcaoParede);

                if (!crossProduct.IsAlmostEqualTo(XYZ.Zero))
                {
                    return true;
                }
            }

            return false;
        }

        private List<BlocoDetectado> DetectarBlocosDoTrecho(Document document, Wall parede, XYZ pontoInicio, XYZ pontoFim, double alturaZCm)
        {
            XYZ direcaoTrecho = (pontoFim - pontoInicio).Normalize();
            XYZ perpendicular = direcaoTrecho.CrossProduct(XYZ.BasisZ).Normalize();

            double meiaFaixa = UnitUtils.ConvertToInternalUnits(2, UnitTypeId.Centimeters);
            double alturaConvertida = UnitUtils.ConvertToInternalUnits(alturaZCm, UnitTypeId.Centimeters);

            XYZ inicioNaFiada = new XYZ(pontoInicio.X, pontoInicio.Y, pontoInicio.Z + alturaConvertida);
            XYZ fimNaFiada = new XYZ(pontoFim.X, pontoFim.Y, pontoFim.Z + alturaConvertida);

            var cantos = new List<XYZ>
            {
                inicioNaFiada + perpendicular * meiaFaixa,
                inicioNaFiada - perpendicular * meiaFaixa,
                fimNaFiada + perpendicular * meiaFaixa,
                fimNaFiada - perpendicular * meiaFaixa
            };

            double minX = cantos.Min(c => c.X);
            double maxX = cantos.Max(c => c.X);
            double minY = cantos.Min(c => c.Y);
            double maxY = cantos.Max(c => c.Y);
            double minZ = inicioNaFiada.Z - meiaFaixa;
            double maxZ = inicioNaFiada.Z + meiaFaixa;

            Outline outline = new Outline(
                new XYZ(minX, minY, minZ),
                new XYZ(maxX, maxY, maxZ)
            );

            var candidatos = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .WhereElementIsNotElementType()
                .WherePasses(new BoundingBoxIntersectsFilter(outline))
                .Cast<FamilyInstance>()
                .Where(fi => RevitElementoIdentificador.IsBloco(fi))
                .Where(fi =>
                {
                    XYZ facing = fi.FacingOrientation.Normalize();
                    XYZ cross = facing.CrossProduct(parede.Orientation);
                    return cross.IsAlmostEqualTo(XYZ.Zero);
                })
                .ToList();

            var blocosDetectados = new List<BlocoDetectado>();

            foreach (var candidato in candidatos)
            {
                if (candidato.Location is not LocationPoint locationPoint)
                    continue;

                XYZ posicaoBloco = locationPoint.Point;
                double posicaoX = (posicaoBloco - pontoInicio).DotProduct(direcaoTrecho);
                string tipo = candidato.Symbol.Name;

                blocosDetectados.Add(new BlocoDetectado(tipo, posicaoX));
            }

            return blocosDetectados;
        }

        private List<AberturaDetectada> DetectarAberturasDoTrecho(Wall parede, XYZ pontoInicio, XYZ pontoFim)
        {
            XYZ direcaoTrecho = (pontoFim - pontoInicio).Normalize();
            double comprimentoTrecho = pontoInicio.DistanceTo(pontoFim);

            var filtro = new ElementMulticategoryFilter(
                new List<BuiltInCategory> { BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows });

            var aberturas = parede.GetDependentElements(filtro)
                .Select(id => parede.Document.GetElement(id))
                .Cast<FamilyInstance>()
                .ToList();

            var aberturasDetectadas = new List<AberturaDetectada>();

            foreach (var abertura in aberturas)
            {
                if (abertura.Location is not LocationPoint locationPoint)
                    continue;

                XYZ posicaoAbertura = locationPoint.Point;
                double posicaoXCentro = (posicaoAbertura - pontoInicio).DotProduct(direcaoTrecho);

                if (posicaoXCentro < 0 || posicaoXCentro > comprimentoTrecho)
                    continue;

                double comprimentoAbertura = ObterComprimentoAbertura(abertura);

                aberturasDetectadas.Add(new AberturaDetectada(posicaoXCentro, comprimentoAbertura));
            }

            return aberturasDetectadas;
        }

        private double ObterComprimentoAbertura(FamilyInstance abertura)
        {
            var parametro = abertura.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH)
                ?? abertura.Symbol.get_Parameter(BuiltInParameter.WINDOW_WIDTH);

            return parametro?.AsDouble() ?? 0;
        }

        private TrechoDados MontarTrechoDados(Document document, Wall parede, TrechoBruto trecho)
        {
            XYZ pontoInicio = trecho.PontoInicio.Coordenada;
            XYZ pontoFim = trecho.PontoFim.Coordenada;

            double espessuraParede = parede.Width;
            double comprimentoTrecho = pontoInicio.DistanceTo(pontoFim);

            var blocosFiada1 = DetectarBlocosDoTrecho(document, parede, pontoInicio, pontoFim, 9)
                .Select(b => new BlocoNoTrecho(b.Tipo, b.PosicaoX))
                .ToList();

            var blocosFiada2 = DetectarBlocosDoTrecho(document, parede, pontoInicio, pontoFim, 29)
                .Select(b => new BlocoNoTrecho(b.Tipo, b.PosicaoX))
                .ToList();

            var aberturas = DetectarAberturasDoTrecho(parede, pontoInicio, pontoFim)
                .Select(a => new AberturaNoTrecho(a.PosicaoXCentro, a.Comprimento))
                .ToList();

            return new TrechoDados(
                espessuraParede,
                comprimentoTrecho,
                trecho.InicioEhLivre,
                trecho.FimEhLivre,
                trecho.PontoInicio.AmarracaoFiada1,
                trecho.PontoInicio.AmarracaoFiada2,
                trecho.PontoFim.AmarracaoFiada1,
                trecho.PontoFim.AmarracaoFiada2,
                aberturas,
                blocosFiada1,
                blocosFiada2);
        }
    }
}