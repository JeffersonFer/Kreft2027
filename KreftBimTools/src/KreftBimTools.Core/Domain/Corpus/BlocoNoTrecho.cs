namespace KreftBimTools.Core.Domain.Corpus;

public class BlocoNoTrecho
{
    public string Tipo { get; }
    public double PosicaoX { get; }

    public BlocoNoTrecho(string tipo, double posicaoX)
    {
        Tipo = tipo;
        PosicaoX = posicaoX;
    }
}