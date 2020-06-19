using GlobalPayments.Api.Utils;

namespace GlobalPayments.Api.Entities.GpApi
{
    public abstract class GpApiEntity {
        internal abstract void FromJson(JsonDoc doc);
    }
}
