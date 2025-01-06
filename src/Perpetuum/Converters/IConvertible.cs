
namespace Perpetuum.Converters
{
    public interface IConvertible<out T>
    {
        T ConvertTo();
    }
}