
using Common.Results;

namespace Common
{
    public interface IResultObjectPagedListModel<TData> : IResultObjectListModel<TData>, IResultPagedListModel
         where TData : class, new()
    {

    }
}
