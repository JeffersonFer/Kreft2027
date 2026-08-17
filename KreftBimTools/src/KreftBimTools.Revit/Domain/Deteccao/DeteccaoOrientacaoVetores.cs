using Autodesk.Revit.DB;
using KreftBimTools.Core.Deteccao;
using KreftBimTools.Core.Domain;

namespace KreftBimTools.Revit.Domain.Deteccao
{
    public class DeteccaoOrientacaoVetores : IEstrategiaRefinamento
    {
        private readonly Autodesk.Revit.DB.Document _doc;

        public DeteccaoOrientacaoVetores(Document doc)
        {
            _doc = doc;
        }

        public IEnumerable<IElementoAlvenaria> Refinar(IElementoAlvenaria elementoAlvenaria, IEnumerable<IElementoAlvenaria> candidatos)
        {
            var elementId = new ElementId(long.Parse(elementoAlvenaria.Identificador));
            Element elementDetector = _doc.GetElement(elementId);

            var orientacaoDoDetector = RevitElementoIdentificador.ObterOrientacao(elementDetector);

            List<IElementoAlvenaria> entidades = new List<IElementoAlvenaria>();

            foreach (var candidato in candidatos)
            {
                var candidatoId = new ElementId(long.Parse(candidato.Identificador));
                Element elementCandidato = _doc.GetElement(candidatoId);

                var orientacaoDoCandidato = RevitElementoIdentificador.ObterOrientacao(elementCandidato);

                bool IsParalelo = orientacaoDoCandidato.EixoY.CrossProduct(orientacaoDoDetector.EixoY).IsAlmostEqualTo(XYZ.Zero);

                if (IsParalelo)
                {
                    entidades.Add(candidato);
                }
            }

            return entidades;
        }
    }
}
