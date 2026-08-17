using Autodesk.Revit.DB;
using KreftBimTools.Core.Domain;

namespace KreftBimTools.Revit.Domain
{
    internal static class RevitElementoIdentificador
    {
        private static readonly Dictionary<string, TipoElementoAlvenaria> _mapaTipos = new()
        {
            ["Estrutural"] = TipoElementoAlvenaria.Estrutural,
            ["Bloco"] = TipoElementoAlvenaria.Bloco,
            ["Graute Vertical"] = TipoElementoAlvenaria.GrauteVertical,
            ["Graute Horizontal"] = TipoElementoAlvenaria.GrauteHorizontal,
            ["Porta"] = TipoElementoAlvenaria.Porta,
            ["Janela"] = TipoElementoAlvenaria.Janela,
            ["Viga"] = TipoElementoAlvenaria.Viga,
            ["Pilar"] = TipoElementoAlvenaria.Pilar,
        };

        /// <summary>
        /// Pega o conteúdo parâmetro TypeComments do elemento passado no método
        /// </summary>
        /// <param name="element"></param>
        /// <returns>O valor do TypeComments</returns>
        public static string? ObterTypeComments(Element element)
        {
            if (element is Wall wall)
            {
                return wall.WallType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS)?.AsString();
            }

            if (element is FamilyInstance familyInstance)
            {
                return familyInstance.Symbol?.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS)?.AsString();
            }

            return null;
        }

        public static TipoElementoAlvenaria ObterTipo(Element element)
        {
            var typeComments = ObterTypeComments(element);

            if(typeComments != null && _mapaTipos.TryGetValue(typeComments, out var tipo))
            {
                return tipo;
            }

            return TipoElementoAlvenaria.Desconhecido;
        }

        /// <summary>
        /// Verifica se o elemento é uma parede do tipo "Estrutural", 
        /// conforme identificado pelo parâmetro TypeComments do seu tipo.
        /// </summary>
        /// <param name="element">Elemento do Revit a ser verificado.</param>
        /// <returns>True se for uma parede estrutural; caso contrário, false.</returns>
        public static bool IsParedeEstrutural(Element element)
            => element is Wall && ObterTipo(element) == TipoElementoAlvenaria.Estrutural;

        public static bool IsBloco(Element element)
            => ObterTipo(element) == TipoElementoAlvenaria.Bloco;

        public static OrientacaoElemento? ObterOrientacao(Element element)
        {
            if(element is Wall wall)
            {
                var orientacao = wall.Orientation;
                return new OrientacaoElemento(XYZ.BasisZ.CrossProduct(orientacao), orientacao, XYZ.BasisZ);
            }

            if(element is FamilyInstance familyInstance)
            {
                var tranform = familyInstance.GetTotalTransform();
                return new OrientacaoElemento(tranform.BasisX, tranform.BasisY, tranform.BasisZ);
            }

            return null;
        }
    }
}