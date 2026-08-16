using Autodesk.Revit.DB;
using KreftBimTools.Core.Application;
using KreftBimTools.Core.Domain;
using KreftBimTools.Core.Domain.Entities;

namespace KreftBimTools.Revit.Domain
{
    internal class RevitElementoFactory : IElementoFactory
    {
        public IElementoAlvenaria? Criar(object elementoBruto)
        {
            if (elementoBruto is not Element element)
                return null;

            if (RevitElementoIdentificador.IsParedeEstrutural(element))
                return new Parede(element.Id.ToString());

            if (RevitElementoIdentificador.IsBloco(element))
                return new Bloco(element.Id.ToString());

            return null;
        }
    }
}