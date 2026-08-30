namespace KreftBimTools.Revit.Domain.Corpus
{
    public class TrechoBruto
    {
        public PontoDeParede PontoInicio { get; }
        public PontoDeParede PontoFim { get; }
        public bool InicioEhLivre { get; }
        public bool FimEhLivre { get; }

        public TrechoBruto(PontoDeParede pontoInicio, PontoDeParede pontoFim, bool inicioEhLivre, bool fimEhLivre)
        {
            PontoInicio = pontoInicio;
            PontoFim = pontoFim;
            InicioEhLivre = inicioEhLivre;
            FimEhLivre = fimEhLivre;
        }
    }
}