using Entities;

namespace Demo.BlazorWasm;

public class Examples
{
    // Create generated entities, based on DDD.UbiquitousLanguageRegistry.txt
    public object[] CreateEntities()
    {
        return
        [
            new Customer(),
            new Employee(),
            new Product(),
            new Shop(),
            new Stock()
        ];
    }

    // Execute generated method Report
    public IEnumerable<string> CreateEntityReport(SampleEntity entity)
    {
        // return entity.Report();
        return [];
    }
}
