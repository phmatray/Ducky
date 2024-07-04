namespace R3dux;

public interface IStore
{
    bool IsInitialized { get; }
    IDispatcher GetDispatcher();
    void Dispatch(object action);
}