using MetaAdsConnector.DTO_s;
using MetaAdsConnector.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using MetaAdsConnector.Data;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsConnector.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LeadsController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        private const string CrmUrl = "https://my.betatrd.com/api/affiliate/register";
        private const string CrmToken = "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.eyJhdWQiOiIxIiwianRpIjoiMGNhMGI1ZjJhMWVjZDY5ODlmNmIxMWU2NzIwNzgyNTcwYzAxZjU4YWE1NWJhZjhlZTc2YmUyYTcxMDQ0ODE0ODQ3MzljZDk1ODQxMThmYTEiLCJpYXQiOjE3NDY0NDQ5ODguNzY5NTgxLCJuYmYiOjE3NDY0NDQ5ODguNzY5NTg1LCJleHAiOjE3Nzc5ODA5ODguNzYzODQ2LCJzdWIiOiIxMDY1NDkiLCJzY29wZXMiOltdLCJpcHY0IjoiMTU0LjQ3LjI0LjIwMiJ9.AY0Mqgm1kqWqkvLHNjeKHHq9asJoZWAJwl-r52IsglrGY5Q6pk_PthRkOFRRJMDDKS9yHvRO_x_3kVVYiLn_Sf15DpwYsl6iwe-7Ra16YYpo-pZtrktsQDp3ZX0BHkVheF50AlJHFRtUMc6W6FHk7kRWEe0dY7pPTQvSrM8h1OQbg_yf_WlWZrCI9D9tSdjCXWHmcik51IGegaO4NuCrm3nwc6_JTJbGMq7uxwZNjbgWTE6orHDdvI-XJG1vUbMZBvHLeYK_SiXJJ4NgWNfPjNJC9Dae2xB4SXUDTKeGSRCY22g0_tLfRnBBRBvPBRFXcairQcw6-U7uzssH3CE5OtxO3l-Cbkv82RreXKBD1qs8NgiODK0Qe7PHDBkY9vjr8-EYpBl_BcReOLVjjfC3iL5n38nzDsxuOpqIGpJpU8SuAwCwZYZ9EtKwOxPpmXUxJcFJ90oKqRkhufmSOOt1uq8jdJBov7ToymHsMUQ-VI5ziNAvgzhY3uWkK2gh0tuIpA_xch9AQPazrQDTyoavFQI_LNpsiPQKC6DGQUFjDwbfv0AjPcSTb4XCatBvff6mC805BLLCDtqMsgOCKTLOxBmSxYyMEAgSUgLwBX8kAqcDQ759RNYSjVMyB5c0MM0bTF_Qj4f0UDWshdbgBKo2w5vPlqmH47fR5lUEsR6RRFA"; // your full token

        public LeadsController(DataContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveLead([FromBody] CreateLeadDto dto)
        {
            var lead = new Lead { Fields = new List<LeadField>() };

            // Prepare payload to send to CRM
            var crmPayload = new Dictionary<string, string>();

            foreach (var pair in dto.Fields)
            {
                lead.Fields.Add(new LeadField
                {
                    FieldName = pair.Key,
                    FieldValue = pair.Value
                });

                crmPayload[pair.Key] = pair.Value;
            }

            // Send POST to CRM
            var httpClient = _httpClientFactory.CreateClient();

            var json = JsonSerializer.Serialize(crmPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CrmToken.Split(' ').Last());

            // Send POST to CRM
            var response = await httpClient.PostAsync(CrmUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, $"CRM rejected the lead. Response: {responseBody}");
            }

            if (!response.Content.Headers.ContentType?.MediaType.Contains("application/json") ?? true)
            {
                return StatusCode(502, $"Unexpected CRM response: {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var uuid = doc.RootElement.GetProperty("client_id").GetString();



            lead.Uuid = uuid!;
            await _context.Leads.AddAsync(lead);
            await _context.SaveChangesAsync();

            return Ok(new { uuid });
        }


        [HttpGet("{uuid}")]
        public async Task<IActionResult> GetLead(string uuid)
        {
            var lead = await _context.Leads
                .Include(l => l.Fields)
                .FirstOrDefaultAsync(l => l.Uuid == uuid);

            if (lead == null)
                return NotFound(new { message = "Lead not found in local database." });

            var crmHttpClient = _httpClientFactory.CreateClient();
            crmHttpClient.DefaultRequestHeaders.Clear();
            crmHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            crmHttpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(CrmToken);

            var combinedFields = lead.Fields.ToDictionary(f => f.FieldName, f => f.FieldValue);

            try
            {
                var crmResponse = await crmHttpClient.GetAsync("https://my.betatrd.com/api/affiliate/register?date_from=&date_to=");

                if (crmResponse.IsSuccessStatusCode)
                {
                    var responseBody = await crmResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseBody);

                    if (doc.RootElement.TryGetProperty("items", out var itemsArray))
                    {
                        var crmLead = itemsArray.EnumerateArray().FirstOrDefault(l =>
                            l.TryGetProperty("uuid", out var cid) && cid.GetString() == uuid);

                        if (crmLead.ValueKind != JsonValueKind.Undefined)
                        {
                            foreach (var prop in crmLead.EnumerateObject())
                            {
                                var key = prop.Name;
                                if (key == "uuid") continue;

                                var value = prop.Value.ToString();
                                combinedFields[key] = value;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fail silently or log error — fields will only include local DB values
            }

            var result = new
            {
                uuid = lead.Uuid,
                createdAt = lead.CreatedAt,
                fields = combinedFields
            };

            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllLeads()
        {
            var localLeads = await _context.Leads
                .Include(l => l.Fields)
                .ToDictionaryAsync(l => l.Uuid, l => l);

            var crmHttpClient = _httpClientFactory.CreateClient();
            crmHttpClient.DefaultRequestHeaders.Clear();
            crmHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            crmHttpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(CrmToken);

            var resultList = new List<object>();
            int page = 1;

            try
            {
                while (true)
                {
                    var crmResponse = await crmHttpClient.GetAsync($"https://my.betatrd.com/api/affiliate/register?date_from=&date_to=&page={page}");

                    if (!crmResponse.IsSuccessStatusCode) break;

                    var responseBody = await crmResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseBody);

                    if (!doc.RootElement.TryGetProperty("items", out var itemsArray) || itemsArray.GetArrayLength() == 0)
                        break;

                    foreach (var item in itemsArray.EnumerateArray())
                    {
                        if (!item.TryGetProperty("uuid", out var uuidProp)) continue;

                        var uuid = uuidProp.GetString();
                        var fields = new Dictionary<string, string>();

                        foreach (var prop in item.EnumerateObject())
                        {
                            if (prop.Name == "uuid") continue;
                            fields[prop.Name] = prop.Value.ToString();
                        }

                        if (!string.IsNullOrEmpty(uuid) && localLeads.TryGetValue(uuid, out var localLead))
                        {
                            // Merge local fields
                            foreach (var f in localLead.Fields)
                                fields[f.FieldName] = f.FieldValue;

                            resultList.Add(new
                            {
                                uuid,
                                createdAt = localLead.CreatedAt,
                                fields
                            });
                        }
                        else
                        {
                            resultList.Add(new
                            {
                                uuid,
                                createdAt = item.TryGetProperty("created_at", out var createdAt) ? createdAt.GetString() : null,
                                fields
                            });
                        }
                    }

                    if (!doc.RootElement.TryGetProperty("links", out var links) ||
                        links.TryGetProperty("next", out var nextLink) && nextLink.ValueKind == JsonValueKind.Null)
                        break;

                    page++;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching CRM data: {ex.Message}");
            }

            return Ok(resultList);
        }

    }

}
