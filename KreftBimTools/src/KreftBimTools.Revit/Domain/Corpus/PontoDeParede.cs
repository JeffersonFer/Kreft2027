using Autodesk.Revit.DB;

namespace KreftBimTools.Revit.Domain.Corpus
{
    public class PontoDeParede
    {
        public XYZ Coordenada { get; }
        public string Rotulo { get; }
        public bool AmarracaoFiada1 { get; }
        public bool AmarracaoFiada2 { get; }

        public PontoDeParede(XYZ coordenada, string rotulo, bool amarracaoFiada1, bool amarracaoFiada2)
        {
            Coordenada = coordenada;
            Rotulo = rotulo;
            AmarracaoFiada1 = amarracaoFiada1;
            AmarracaoFiada2 = amarracaoFiada2;
        }
    }
}