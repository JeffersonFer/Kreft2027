namespace KreftBimTools.Core.Domain.Entities
{
    public class Parede : IElementoAlvenaria
    {
        public string Identificador { get; }

        public Parede(string identificador)
        {
            Identificador = identificador;
        }
    }
}
