using KreftBimTools.Core.Deteccao;
using KreftBimTools.Core.Domain;

namespace KreftBimTools.Core.Application
{
    public class AgrupadorDeParedeService
    {
        private readonly IEstrategiaDeteccao _deteccaoBoundingBox;
        private readonly IEstrategiaDeteccao _deteccaoInserts;

        public AgrupadorDeParedeService(
            IEstrategiaDeteccao deteccaoBoundingBox,
            IEstrategiaDeteccao deteccaoInserts)
        {
            _deteccaoBoundingBox = deteccaoBoundingBox;
            _deteccaoInserts = deteccaoInserts;
        }

        public IEnumerable<IElementoAlvenaria> AgruparElementosDaParede(IElementoAlvenaria parede)
        {
            var elementos = new List<IElementoAlvenaria>();

            elementos.AddRange(_deteccaoBoundingBox.Detectar(parede));  // Blocos, Grautes Horizontais...
            elementos.AddRange(_deteccaoInserts.Detectar(parede));       // Portas, Janelas

            return elementos;
        }
    }
}
