using Common.Results;
using Common.Results.BaseClasses;

namespace Common
{
    public abstract class BaseResultValueModel<TData> : BaseResultModel, IResultValueModel<TData>
    {
        public TData? Value { get; set; }
    }
}
