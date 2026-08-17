using Autodesk.Revit.DB;

namespace KreftBimTools.Revit.Domain
{
    public class OrientacaoElemento
    {
        public XYZ EixoX {  get; }
        public XYZ EixoY { get; }
        public XYZ EixoZ { get; }

        public OrientacaoElemento(XYZ eixoX, XYZ eixoY, XYZ eixoZ)
        {
            EixoX = eixoX;
            EixoY = eixoY;
            EixoZ = eixoZ;
        }
    }
}
