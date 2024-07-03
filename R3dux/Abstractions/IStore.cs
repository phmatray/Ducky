namespace R3dux;

public interface IStore
{
    bool IsInitialized { get; }
    RootState GetState();
    IDispatcher GetDispatcher();
    void Dispatch(object action);
}