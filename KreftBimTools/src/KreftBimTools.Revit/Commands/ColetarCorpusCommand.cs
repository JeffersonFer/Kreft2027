using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KreftBimTools.Revit.Domain;
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
                var pontos = ObterPontosDeIntersecao(doc, wall);

                var textoPontos = string.Join("\n", pontos.Select(p => $"  ({p.X:F2}, {p.Y:F2}, {p.Z:F2})"));
                resumo.Add($"Parede {wall.Id}: {pontos.Count} ponto(s)\n{textoPontos}");
            }

            TaskDialog.Show("Coletor de Corpus", string.Join("\n\n", resumo));

            return Result.Succeeded;
        }

        private List<XYZ> ObterPontosDeIntersecao(Document document, Wall paredeAlvo)
        {
            var curvaAlvo = (paredeAlvo.Location as LocationCurve)?.Curve;
            if (curvaAlvo == null)
                return new List<XYZ>();

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

            // Remove pontos intermediários que coincidem com PI ou PF (dentro de uma tolerância)
            var pontosFiltrados = pontosIntermediarios
                .Where(p => !p.IsAlmostEqualTo(pontoInicial) && !p.IsAlmostEqualTo(pontoFinal))
                .ToList();

            var pontosOrdenados = pontosFiltrados
                .OrderBy(p => curvaAlvo.Project(p).Parameter)
                .ToList();

            var todosOsPontos = new List<XYZ> { pontoInicial };
            todosOsPontos.AddRange(pontosOrdenados);
            todosOsPontos.Add(pontoFinal);

            return todosOsPontos;
        }
    }
}