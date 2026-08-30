namespace KreftBimTools.Core.Domain.Corpus;

public class AberturaNoTrecho
{
    public double PosicaoXCentro { get; }
    public double Comprimento { get; }

    public AberturaNoTrecho(double posicaoXCentro, double comprimento)
    {
        PosicaoXCentro = posicaoXCentro;
        Comprimento = comprimento;
    }
}