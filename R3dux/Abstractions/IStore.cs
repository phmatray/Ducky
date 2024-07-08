using R3dux.Temp;

namespace R3dux;

public interface IStore
{
    RootState GetRootState();
    
    void Dispatch(IAction action);
    
    void AddSlice(ISlice slice);
    void AddSlices(IEnumerable<ISlice> slices);
    
    void AddEffect(IEffect effect);
    void AddEffects(IEnumerable<IEffect> effects);
}