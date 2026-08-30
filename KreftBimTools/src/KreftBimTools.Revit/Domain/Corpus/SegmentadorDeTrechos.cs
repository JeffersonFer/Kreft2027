using System.Collections.Generic;

namespace KreftBimTools.Revit.Domain.Corpus
{
    public static class SegmentadorDeTrechos
    {
        public static List<TrechoBruto> Segmentar(List<PontoDeParede> pontos)
        {
            var trechos = new List<TrechoBruto>();

            for (int i = 0; i < pontos.Count - 1; i++)
            {
                var pontoA = pontos[i];
                var pontoB = pontos[i + 1];

                bool aEhLivre = EhExtremidadeLivre(pontoA.Rotulo);
                bool bEhLivre = EhExtremidadeLivre(pontoB.Rotulo);

                trechos.Add(new TrechoBruto(pontoA, pontoB, aEhLivre, bEhLivre));
            }

            return trechos;
        }

        private static bool EhExtremidadeLivre(string rotulo)
        {
            return rotulo == "PIL" || rotulo == "PFL";
        }
    }
}