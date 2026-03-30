using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoEnrollmentCheckServiceTests
{
    [Fact]
    public async Task CheckEnrollmentAsync_WhenRequestHasNoChildren_ReturnsEmptyResults()
    {
        var service = new ColoradoEnrollmentCheckService();
        var request = new EnrollmentCheckRequest
        {
            Children = new List<ChildCheckRequest>()
        };

        var result = await service.CheckEnrollmentAsync(request);

        Assert.NotNull(result);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_WhenNoApiConfiguration_ThrowsInvalidOperationException()
    {
        var service = new ColoradoEnrollmentCheckService();
        var request = new EnrollmentCheckRequest
        {
            Children = new List<ChildCheckRequest>
            {
                new ChildCheckRequest
                {
                    CheckId = Guid.NewGuid(),
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = new DateOnly(2015, 3, 12),
                    SchoolName = "Lincoln Elementary"
                }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CheckEnrollmentAsync(request));
    }
}
