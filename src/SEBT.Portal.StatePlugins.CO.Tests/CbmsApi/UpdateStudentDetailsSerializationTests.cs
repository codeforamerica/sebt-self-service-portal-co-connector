using System.Text.Json;
using Microsoft.Kiota.Abstractions.Serialization;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

public class UpdateStudentDetailsSerializationTests
{
    [Fact]
    public async Task Deserialize_UpdateStudentDetailsResponse()
    {
        // Arrange — JSON from the CBMS API spec's example response
        var json = """
            {
              "respCd": "200",
              "respMsg": "Success"
            }
            """;

        // Act
        var result = await KiotaJsonSerializer.DeserializeAsync<UpdateStudentDetailsResponse>(
            json, UpdateStudentDetailsResponse.CreateFromDiscriminatorValue);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("200", result.RespCd);
        Assert.Equal("Success", result.RespMsg);
    }

    [Fact]
    public async Task Serialize_UpdateStudentDetailsRequest()
    {
        // Arrange
        var request = new UpdateStudentDetailsRequest
        {
            SebtChldId = "CH-88291",
            SebtAppId = "APP-12345",
            Addr = new Address
            {
                AddrLn1 = "456 Oak Street",
                AddrLn2 = "Suite 200",
                Cty = "Denver",
                StaCd = "CO",
                Zip = "80202",
                Zip4 = "4421"
            },
            ReqNewCard = "Y",
            OptOut = "N",
            GurdFstNm = "Jane",
            GurdLstNm = "Smith",
            GurdEmailAddr = "jane.smith@example.com",
            NtfnOptInSw = "Y",
            NtfnSrc = "email"
        };

        // Act
        var json = await KiotaJsonSerializer.SerializeAsStringAsync(request);

        // Assert — verify wire-format field names match the spec
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("CH-88291", root.GetProperty("sebtChldId").GetString());
        Assert.Equal("APP-12345", root.GetProperty("sebtAppId").GetString());
        Assert.Equal("Y", root.GetProperty("reqNewCard").GetString());
        Assert.Equal("N", root.GetProperty("optOut").GetString());
        Assert.Equal("Jane", root.GetProperty("gurdFstNm").GetString());
        Assert.Equal("Smith", root.GetProperty("gurdLstNm").GetString());
        Assert.Equal("jane.smith@example.com", root.GetProperty("gurdEmailAddr").GetString());
        Assert.Equal("Y", root.GetProperty("ntfnOptInSw").GetString());
        Assert.Equal("email", root.GetProperty("ntfnSrc").GetString());

        // Verify nested address object
        var addr = root.GetProperty("addr");
        Assert.Equal("456 Oak Street", addr.GetProperty("addrLn1").GetString());
        Assert.Equal("Suite 200", addr.GetProperty("addrLn2").GetString());
        Assert.Equal("Denver", addr.GetProperty("cty").GetString());
        Assert.Equal("CO", addr.GetProperty("staCd").GetString());
        Assert.Equal("80202", addr.GetProperty("zip").GetString());
        Assert.Equal("4421", addr.GetProperty("zip4").GetString());
    }
}
