using KreftBimTools.Core.Domain;

namespace KreftBimTools.Core.Application
{
    public interface IElementoFactory
    {
        IElementoAlvenaria? Criar(object elementoBruto);
    }
}
