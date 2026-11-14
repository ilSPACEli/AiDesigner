using AiDesigner.Repositories;

namespace AiDesigner.Tests.Repositories;

public class InMemoryResultRepositoryTests : ResultRepositoryContractTests
{
    protected override IResultRepository CreateRepository() => new InMemoryResultRepository();
}