using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace KreftBimTools.Revit.Domain.SelectionFilters
{
    internal class ParedeEstruturalFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => RevitElementoIdentificador.IsParedeEstrutural(elem);

        public bool AllowReference(Reference reference, XYZ position) => false;
        
        
    }
}
