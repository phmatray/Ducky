namespace Spike.Lib;

public sealed record RefA(int N);
public sealed record RefB(string S);

public union RefAction(RefA, RefB);
