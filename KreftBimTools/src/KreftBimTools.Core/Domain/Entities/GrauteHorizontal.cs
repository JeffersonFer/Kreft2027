namespace KreftBimTools.Core.Domain.Entities
{
    public class GrauteHorizontal : IElementoAlvenaria
    {
        public string Identificador { get; }

        public GrauteHorizontal(string identificador)
        {
            Identificador = identificador;
        }
    }
}
