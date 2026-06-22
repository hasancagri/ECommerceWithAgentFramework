using Common.Results.BaseClasses;

namespace Common
{
    public abstract class BaseResultObjectModel<TData> : BaseResultModel, IResultObjectModel<TData>
    where TData : class, new()
    {
        protected BaseResultObjectModel()
        {
            Data = new TData();
        }
        
        public TData Data { get; set; }
        
    }
}
