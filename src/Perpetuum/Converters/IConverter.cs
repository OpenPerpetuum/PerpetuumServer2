namespace Perpetuum.Converters
{
    public interface IConverter<in TIn, out TOut>
    {
        TOut Convert(TIn item);
    }
}