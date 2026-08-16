using KreftBimTools.Core.Domain;

namespace KreftBimTools.Core.Deteccao
{
    public interface IEstrategiaDeteccao
    {
        IEnumerable<IElementoAlvenaria> Detectar(IElementoAlvenaria elementoAlvenaria);
    }
}
