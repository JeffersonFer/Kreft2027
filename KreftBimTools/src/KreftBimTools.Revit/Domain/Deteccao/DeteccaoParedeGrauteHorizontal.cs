using Autodesk.Revit.DB;
using KreftBimTools.Core.Deteccao;
using KreftBimTools.Core.Domain;

namespace KreftBimTools.Revit.Domain.Deteccao
{
    public class DeteccaoParedeGrauteHorizontal : IEstrategiaDeteccao
    {
        private readonly Document _doc;
        private readonly double _tolerancia = 0.1;

        public DeteccaoParedeGrauteHorizontal(Document doc)
        {
            _doc = doc;
        }

        public IEnumerable<IElementoAlvenaria> Detectar(IElementoAlvenaria elementoAlvenaria)
        {
            var elementId = new ElementId(long.Parse(elementoAlvenaria.Identificador));
            Element elementDetector = _doc.GetElement(elementId);

            BoundingBoxXYZ elementDetectorBB = elementDetector.get_BoundingBox(null);
            Outline outline = new Outline(
                elementDetectorBB.Min - new XYZ(_tolerancia, _tolerancia, _tolerancia),
                elementDetectorBB.Max + new XYZ(_tolerancia, _tolerancia, _tolerancia)
            );

            BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(outline);

            // Sem ElementCategoryFilter aqui - categoria e TypeComments filtrados em memória
            var candidatos = new FilteredElementCollector(_doc)
                .WherePasses(bbFilter)
                .WhereElementIsNotElementType()
                .Where(e => e.Id != elementDetector.Id)
                .Where(e => RevitElementoIdentificador.IsGrauteHorizontal(e))
                .ToList();

            var factory = new RevitElementoFactory();
            var entidades = new List<IElementoAlvenaria>();

            foreach (var element in candidatos)
            {
                var entidade = factory.Criar(element);
                if (entidade != null)
                    entidades.Add(entidade);
            }

            return entidades;
        }
    }
}