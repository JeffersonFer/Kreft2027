using KreftBimTools.Core.Deteccao;
using KreftBimTools.Core.Domain;
using System.Reflection.Metadata;

namespace KreftBimTools.Revit.Domain.Deteccao
{
    public class DeteccaoSolidOrigin : IEstrategiaRefinamento
    {
        private readonly Document _doc;

        public DeteccaoSolidOrigin(Document doc){
            _doc = doc;
        }

        public IEnumerable<IElementoAlvenaria> Refinar(IElementoAlvenaria elementoAlvenaria, IEnumerable<IElementoAlvenaria> candidatos)
        {
            throw new NotImplementedException();
        }
    }
}
