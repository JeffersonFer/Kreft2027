namespace KreftBimTools.Core.Domain.Entities
{
    public class Bloco : IElementoAlvenaria
    {
        public string Identificador { get; }

        public Bloco(string identificador)
        {
            Identificador = identificador;
        }
    }
}
