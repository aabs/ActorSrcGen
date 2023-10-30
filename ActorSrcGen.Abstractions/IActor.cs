namespace ActorSrcGen;

public interface IActor<T1>
{
    bool Call(T1 arg1);
    Task<bool> Cast(T1 arg1);
}
