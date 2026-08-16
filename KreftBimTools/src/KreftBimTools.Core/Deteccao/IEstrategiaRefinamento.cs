using KreftBimTools.Core.Domain;

namespace KreftBimTools.Core.Deteccao
{
    public interface IEstrategiaRefinamento
    {
        IEnumerable<IElementoAlvenaria> Refinar(
        IElementoAlvenaria elementoAlvenaria,
        IEnumerable<IElementoAlvenaria> candidatos);
    }
}
