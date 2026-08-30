using System.Collections.Generic;

namespace KreftBimTools.Core.Domain.Corpus;

public class TrechoDados
{
    public double EspessuraParede { get; }
    public double ComprimentoTrecho { get; }
    public bool InicioEhLivre { get; }
    public bool FimEhLivre { get; }
    public bool AmarracaoFiada1NoInicio { get; }
    public bool AmarracaoFiada2NoInicio { get; }
    public bool AmarracaoFiada1NoFim { get; }
    public bool AmarracaoFiada2NoFim { get; }
    public List<AberturaNoTrecho> Aberturas { get; }
    public List<BlocoNoTrecho> Fiada1 { get; }
    public List<BlocoNoTrecho> Fiada2 { get; }

    public TrechoDados(
        double espessuraParede,
        double comprimentoTrecho,
        bool inicioEhLivre,
        bool fimEhLivre,
        bool amarracaoFiada1NoInicio,
        bool amarracaoFiada2NoInicio,
        bool amarracaoFiada1NoFim,
        bool amarracaoFiada2NoFim,
        List<AberturaNoTrecho> aberturas,
        List<BlocoNoTrecho> fiada1,
        List<BlocoNoTrecho> fiada2)
    {
        EspessuraParede = espessuraParede;
        ComprimentoTrecho = comprimentoTrecho;
        InicioEhLivre = inicioEhLivre;
        FimEhLivre = fimEhLivre;
        AmarracaoFiada1NoInicio = amarracaoFiada1NoInicio;
        AmarracaoFiada2NoInicio = amarracaoFiada2NoInicio;
        AmarracaoFiada1NoFim = amarracaoFiada1NoFim;
        AmarracaoFiada2NoFim = amarracaoFiada2NoFim;
        Aberturas = aberturas;
        Fiada1 = fiada1;
        Fiada2 = fiada2;
    }
}