using Autodesk.Revit.DB;
using KreftBimTools.Core.Deteccao;
using KreftBimTools.Core.Domain;

namespace KreftBimTools.Revit.Domain.Deteccao
{
    public class DeteccaoBoundingBoxBoundingBox : IEstrategiaDeteccao
    {
        private readonly Autodesk.Revit.DB.Document _doc;
        private BuiltInCategory _category;
        private string _typeComments;
        private double _tolerancia = 0.1;

        public DeteccaoBoundingBoxBoundingBox(Autodesk.Revit.DB.Document doc,
            BuiltInCategory category,
            string typeComments)
        {
            _doc = doc;
            _category = category;
            _typeComments = typeComments;
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

            // Filtro espacial: elementos dentro/intersectando a bounding box
            BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(outline);

            // Filtro de categoria: apenas Modelos Genéricos
            ElementCategoryFilter categoryFilter =
                new ElementCategoryFilter(_category);

            // Combinar os dois quick filters com LogicalAndFilter
            LogicalAndFilter quickFilters =
                new LogicalAndFilter(bbFilter, categoryFilter);

            // Filtro de parâmetro de tipo: ALL_MODEL_TYPE_COMMENTS == "Bloco"
            ParameterValueProvider pvp =
                new ParameterValueProvider(
                    new ElementId((long)BuiltInParameter.ALL_MODEL_TYPE_COMMENTS));

            FilterStringRule rule =
                new FilterStringRule(pvp, new FilterStringEquals(), _typeComments);

            ElementParameterFilter paramFilter =
                new ElementParameterFilter(rule);

            IList<Element> result = new FilteredElementCollector(_doc)
                .WherePasses(quickFilters)       // quick filters primeiro
                .WherePasses(paramFilter)        // slow filter depois
                .WhereElementIsNotElementType()  // apenas instâncias
                .Where(e => e.Id != elementDetector.Id) // excluir o próprio target
                .ToList();

            var factory = new RevitElementoFactory();
            var entidades = new List<IElementoAlvenaria>();                

            foreach (var element in result)
            {
                var entidade = factory.Criar(element);
                if(entidade != null )
                {
                    entidades.Add(entidade);
                }
            }

            return entidades;
        }
    }
}
