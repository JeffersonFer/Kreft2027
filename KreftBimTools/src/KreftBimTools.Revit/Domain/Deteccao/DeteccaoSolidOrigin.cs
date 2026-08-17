using Autodesk.Revit.DB;
using KreftBimTools.Core.Deteccao;
using KreftBimTools.Core.Domain;

namespace KreftBimTools.Revit.Domain.Deteccao
{
    public class DeteccaoSolidOrigin : IEstrategiaRefinamento
    {
        private readonly Document _doc;

        public DeteccaoSolidOrigin(Document doc)
        {
            _doc = doc;
        }

        public IEnumerable<IElementoAlvenaria> Refinar(IElementoAlvenaria elementoAlvenaria, IEnumerable<IElementoAlvenaria> candidatos)
        {
            var elementId = new ElementId(long.Parse(elementoAlvenaria.Identificador));
            var elementDetector = _doc.GetElement(elementId);
            List<Solid> elementDetectorSolidos = ObterSolidos(elementDetector);

            return ObterEntidades(elementDetectorSolidos, candidatos);
        }

        private List<Solid> ObterSolidos(Element element)
        {
            Options geometryOptions = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = true
            };

            GeometryElement geometryElement = element.get_Geometry(geometryOptions);
            var solidos = new List<Solid>();

            if (geometryElement == null) return solidos;

            foreach (GeometryObject geometryObj in geometryElement)
            {
                if (geometryObj is Solid solid && solid.Volume > 0)
                    solidos.Add(solid);

                if (geometryObj is GeometryInstance geomInstance)
                {
                    foreach (GeometryObject instanceObj in geomInstance.GetInstanceGeometry())
                    {
                        if (instanceObj is Solid instanceSolid && instanceSolid.Volume > 0)
                            solidos.Add(instanceSolid);
                    }
                }
            }

            return solidos;
        }

        private IEnumerable<IElementoAlvenaria> ObterEntidades(List<Solid> elementDetectorSolidos, IEnumerable<IElementoAlvenaria> candidatos)
        {
            List<IElementoAlvenaria> entidades = new List<IElementoAlvenaria>();

            foreach (var candidato in candidatos)
            {
                var candidatoId = new ElementId(long.Parse(candidato.Identificador));
                var elementCandidato = _doc.GetElement(candidatoId);

                var elementCandidatoLocationPoint = elementCandidato.Location as LocationPoint;

                bool estaDentroDeAlgumSolido = elementDetectorSolidos
                    .Any(solido => IsPontoDentroDoSolido(elementCandidatoLocationPoint.Point, solido));

                if (estaDentroDeAlgumSolido)
                {
                    entidades.Add(candidato);
                }
            }

            return entidades;
        }

        private bool IsPontoDentroDoSolido(XYZ ponto, Solid solid)
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
    }
}