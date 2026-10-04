using System.Collections.Immutable;
using System.Text.Json;
using CsCheck;
using Ducky.TestSupport;

namespace Ducky.Tests;

// SPEC §5.10 (IMM-01), ADR-0044; INV-32.
public sealed class EntityStateTests
{
    private const int Keys = 6;

    [Fact]
    public void EntityState_ModelBased()
    {
        // A step is (op, picks, arg): each pick is (id, reuse), reuse taking the stored instance for an existing id (a
        // no-op candidate) per entity, so one batch can mix a fresh and the stored instance of the same id; arg picks the
        // Merge strategy, the RemoveWhere residue and whether Update/Map return the same instance.
        var steps = Gen.Select(Gen.Int[0, 7], Gen.Select(Gen.Int[0, Keys - 1], Gen.Bool).Array[0, 4], Gen.Int[0, 5]).List[16, 48];
        Property.Check(steps, run =>
        {
            var model = new Model();
            var state = EntityState<int, Todo>.Empty;
            var version = 0;
            var edited = new Dictionary<Todo, Todo>(); // the model's and the actual run's edit return one instance
            foreach (var (op, picks, arg) in run)
            {
                var ids = picks.Select(p => p.Item1).ToArray();
                var entities = picks.Select(p => p.Item2 && model.Byid.TryGetValue(p.Item1, out var stored) ? stored : new Todo(p.Item1, ++version)).ToArray();
                Func<Todo, Todo> edit = arg % 2 == 0 ? t => t : t => edited.TryGetValue(t, out var e) ? e : edited[t] = t with { Version = ++version };
                Func<Todo, bool> match = t => t.Id % 3 == arg % 3;
                var strategy = (MergeStrategy)(arg % 3);
                var before = model.Items();
                var expectThrow = false;
                try
                {
                    switch (op)
                    {
                        case 0: model.SetAll(entities); break;
                        case 1: model.Merge(entities, MergeStrategy.FailIfDuplicate); break;
                        case 2: model.Merge(entities, MergeStrategy.PreferIncoming); break;
                        case 3: model.Update(ids.FirstOrDefault(), edit); break;
                        case 4: model.Map(edit); break;
                        case 5: model.Remove(ids); break;
                        case 6: model.RemoveWhere(match); break;
                        default: model.Merge(entities, strategy); break;
                    }
                }
                catch (ArgumentException)
                {
                    expectThrow = true;
                }

                Func<EntityState<int, Todo>> act = op switch
                {
                    0 => () => state.SetAll(entities),
                    1 => () => state.Add(entities),
                    2 => () => state.Upsert(entities),
                    3 => () => state.Update(ids.FirstOrDefault(), edit),
                    4 => () => state.Map(edit),
                    5 => () => state.Remove(ids),
                    6 => () => state.RemoveWhere(match),
                    _ => () => state.Merge(entities, strategy),
                };
                if (expectThrow)
                {
                    Should.Throw<ArgumentException>(act);
                    continue;
                }

                var next = act();
                var after = model.Items();
                if (before.SequenceEqual(after, ReferenceEqualityComparer.Instance))
                {
                    next.ShouldBeSameAs(state);
                }
                else
                {
                    next.ShouldNotBeSameAs(state);
                }

                state = next;
                AssertMatches(state, model);
            }
        });
    }

    [Fact]
    public void EntityState_NoOp_ReturnsSameInstance()
    {
        var a = new Todo(1, 1);
        var b = new Todo(2, 1);
        var state = EntityState<int, Todo>.Empty.Add(a, b);

        state.SetAll([a, b]).ShouldBeSameAs(state);
        state.Add().ShouldBeSameAs(state);
        state.Upsert(a, b).ShouldBeSameAs(state);
        state.Upsert().ShouldBeSameAs(state);
        state.Update(1, t => t).ShouldBeSameAs(state);
        state.Update(3, t => t with { Version = 2 }).ShouldBeSameAs(state);
        state.Map(t => t).ShouldBeSameAs(state);
        state.Remove(3, 4).ShouldBeSameAs(state);
        state.Remove().ShouldBeSameAs(state);
        state.RemoveWhere(t => t.Id > 2).ShouldBeSameAs(state);
        state.Merge([a, b], MergeStrategy.PreferIncoming).ShouldBeSameAs(state);
        state.Upsert(new Todo(1, 9), a).ShouldBeSameAs(state); // a later entity restores the stored instance
        state.Merge([new Todo(1, 9), a], MergeStrategy.PreferIncoming).ShouldBeSameAs(state);
        state.Merge([new Todo(1, 9), new Todo(2, 9)], MergeStrategy.PreferExisting).ShouldBeSameAs(state);
        state.Merge([], MergeStrategy.FailIfDuplicate).ShouldBeSameAs(state);
        EntityState<int, Todo>.Empty.SetAll([]).ShouldBeSameAs(EntityState<int, Todo>.Empty);
    }

    [Fact]
    public void EntityState_JsonRoundTrip()
    {
        var state = EntityState<int, Todo>.Empty.Add(new Todo(2, 1), new Todo(1, 3));

        var json = JsonSerializer.Serialize(state);
        var back = JsonSerializer.Deserialize<EntityState<int, Todo>>(json)!;

        json.ShouldBe("""{"Items":[{"Id":2,"Version":1},{"Id":1,"Version":3}]}""");
        back.Items.ShouldBe(state.Items);
        back.Count.ShouldBe(2);
        back.Find(1).ShouldBe(new Todo(1, 3));
        Should.Throw<ArgumentException>(() => JsonSerializer.Deserialize<EntityState<int, Todo>>("""{"Items":[{"Id":1},{"Id":1}]}"""))
            .Message.ShouldContain("1");
    }

    // Examples for every branch the property reaches (SPEC §17.1: a covered property is never the only witness).
    [Fact]
    public void EntityState_Operations_Examples()
    {
        var a = new Todo(1, 1);
        var b = new Todo(2, 1);
        var c = new Todo(3, 1);
        var state = new EntityState<int, Todo>([a, b]);

        state.Count.ShouldBe(2);
        state.Find(1).ShouldBeSameAs(a);
        state.Find(9).ShouldBeNull();
        state.Contains(2).ShouldBeTrue();
        state.Contains(9).ShouldBeFalse();

        state.SetAll([c, a]).Items.ShouldBe([c, a]);
        state.SetAll([a]).Items.ShouldBe([a]);
        state.Add(c).Items.ShouldBe([a, b, c]);
        state.Upsert(new Todo(1, 2), c, new Todo(3, 2)).Items.ShouldBe([new Todo(1, 2), b, new Todo(3, 2)]);
        state.Update(2, t => t with { Version = 5 }).Items.ShouldBe([a, new Todo(2, 5)]);
        state.Map(t => t.Id == 2 ? t with { Version = 7 } : t).Items.ShouldBe([a, new Todo(2, 7)]);
        state.Remove(1, 9).Items.ShouldBe([b]);
        state.RemoveWhere(t => t.Id == 2).Items.ShouldBe([a]);
        state.Merge([new Todo(1, 4), c, new Todo(3, 4)], MergeStrategy.PreferExisting).Items.ShouldBe([a, b, c]);
        state.Merge([new Todo(1, 4), c], MergeStrategy.PreferIncoming).Items.ShouldBe([new Todo(1, 4), b, c]);
        state.Merge([c], MergeStrategy.FailIfDuplicate).Items.ShouldBe([a, b, c]);

        // The index of a derived instance is its own: positions shift after a removal.
        var removed = state.Remove(1);
        removed.Find(2).ShouldBeSameAs(b);
        removed.Find(1).ShouldBeNull();
        state.Find(1).ShouldBeSameAs(a);
    }

    [Fact]
    public void EntityState_InvalidInput_Throws()
    {
        var a = new Todo(1, 1);
        var state = EntityState<int, Todo>.Empty.Add(a);

        Should.Throw<ArgumentNullException>(() => new EntityState<int, Todo>(null!));
        Should.Throw<ArgumentException>(() => new EntityState<int, Todo>([a, new Todo(1, 2)])).Message.ShouldContain("1");
        Should.Throw<ArgumentException>(() => state.SetAll([new Todo(2, 1), new Todo(2, 2)]));
        Should.Throw<ArgumentException>(() => state.Add(new Todo(1, 2)));
        Should.Throw<ArgumentException>(() => state.Add(new Todo(2, 1), new Todo(2, 2)));
        Should.Throw<ArgumentException>(() => state.Merge([new Todo(1, 2)], MergeStrategy.FailIfDuplicate));
        Should.Throw<ArgumentOutOfRangeException>(() => state.Merge([], (MergeStrategy)3));
        Should.Throw<InvalidOperationException>(() => state.Update(1, t => new Todo(2, 1))).Message.ShouldContain("1");
        Should.Throw<InvalidOperationException>(() => state.Map(t => new Todo(2, 1)));
    }

    // SPEC §5.10: the index is built lazily, once per instance, never per access.
    [Fact]
    public void EntityState_Index_BuiltOncePerInstance()
    {
        var reads = new int[1];
        var state = EntityState<int, Counted>.Empty.Add(new Counted(1, reads), new Counted(2, reads)).Remove(2);
        reads[0] = 0;

        state.Contains(1).ShouldBeTrue();
        var built = reads[0];
        state.Find(1).ShouldNotBeNull();
        state.Contains(2).ShouldBeFalse();

        built.ShouldBe(1);
        reads[0].ShouldBe(built);
    }

    private static void AssertMatches(EntityState<int, Todo> state, Model model)
    {
        state.Items.ShouldBe(model.Items());
        state.Items.Zip(model.Items()).ShouldAllBe(pair => ReferenceEquals(pair.First, pair.Second));
        state.Count.ShouldBe(model.Order.Count);
        for (var id = 0; id < Keys; id++)
        {
            state.Contains(id).ShouldBe(model.Byid.ContainsKey(id));
            state.Find(id).ShouldBeSameAs(model.Byid.GetValueOrDefault(id));
        }
    }

    internal sealed record Todo(int Id, int Version) : IEntity<int>;

    // Counts reads of Id, which only building the index does.
    internal sealed class Counted(int id, int[] reads) : IEntity<int>
    {
        public int Id
        {
            get
            {
                reads[0]++;
                return id;
            }
        }
    }

    // INV-32's reference: a Dictionary plus the insertion-ordered list of ids.
    private sealed class Model
    {
        public Dictionary<int, Todo> Byid { get; } = [];

        public List<int> Order { get; } = [];

        public List<Todo> Items() => [.. Order.Select(id => Byid[id])];

        public void SetAll(Todo[] entities)
        {
            if (entities.DistinctBy(t => t.Id).Count() != entities.Length)
            {
                throw new ArgumentException("duplicate");
            }

            Byid.Clear();
            Order.Clear();
            Merge(entities, MergeStrategy.FailIfDuplicate);
        }

        public void Merge(Todo[] entities, MergeStrategy strategy)
        {
            if (strategy == MergeStrategy.FailIfDuplicate
                && (entities.Any(t => Byid.ContainsKey(t.Id)) || entities.DistinctBy(t => t.Id).Count() != entities.Length))
            {
                throw new ArgumentException("duplicate");
            }

            foreach (var entity in entities)
            {
                if (!Byid.ContainsKey(entity.Id))
                {
                    Order.Add(entity.Id);
                    Byid[entity.Id] = entity;
                }
                else if (strategy == MergeStrategy.PreferIncoming)
                {
                    Byid[entity.Id] = entity;
                }
            }
        }

        public void Update(int id, Func<Todo, Todo> edit)
        {
            if (Byid.TryGetValue(id, out var entity))
            {
                Byid[id] = edit(entity);
            }
        }

        public void Map(Func<Todo, Todo> edit)
        {
            foreach (var id in Order)
            {
                Byid[id] = edit(Byid[id]);
            }
        }

        public void Remove(IEnumerable<int> ids)
        {
            foreach (var id in ids)
            {
                if (Byid.Remove(id))
                {
                    Order.Remove(id);
                }
            }
        }

        public void RemoveWhere(Func<Todo, bool> match) => Remove([.. Order.Where(id => match(Byid[id]))]);
    }
}
