using DataSetChallenge.Models;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DataSetChallenge.ApiClient
{
    public static class VAutoClient
    {
        static HttpClient client = new HttpClient();

        public static void Initialize()
        {
            client.BaseAddress = new Uri("https://vautointerview.azurewebsites.net");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public static string AppendToBaseAddress(string suffix)
        {
            return client.BaseAddress.AbsolutePath + suffix;
        }

        public static async Task<DatasetIdResponse> GetDataSetIdAsync()
        {
            HttpResponseMessage response = await client.GetAsync(client.BaseAddress.AbsoluteUri + "/api/datasetId");
            DatasetIdResponse dataSetId = null;
            if (response.IsSuccessStatusCode)
            {
                dataSetId = await response.Content.ReadAsAsync<DatasetIdResponse>();
            }
            else throw new Exception(string.Format("Unabled to retrieve data id.\n\n{0}", response.ReasonPhrase));
            return dataSetId;
        }

        public static async Task<VehicleIdsResponse> GetVehicleIdsAsync(DatasetIdResponse d)
        {
            HttpResponseMessage response = await client.GetAsync(client.BaseAddress.AbsoluteUri + string.Format("/api/{0}/vehicles", d.datasetId));
            VehicleIdsResponse vehicleIds = null;
            if (response.IsSuccessStatusCode)
            {
                vehicleIds = await response.Content.ReadAsAsync<VehicleIdsResponse>();
            }
            else throw new Exception(string.Format("Unabled to retrieve data vehicle ids.\n\n{0}", response.ReasonPhrase));
            return vehicleIds;
        }

        public static async Task<VehicleResponse> GetVehicleDataAsync(DatasetIdResponse d, int vehicleId)
        {
            HttpResponseMessage response = await client.GetAsync(client.BaseAddress.AbsoluteUri + string.Format("/api/{0}/vehicles/{1}", d.datasetId, vehicleId));
            VehicleResponse vehicleData = null;
            if (response.IsSuccessStatusCode)
            {
                vehicleData = await response.Content.ReadAsAsync<VehicleResponse>();
            }
            else throw new Exception(string.Format("Unabled to retrieve vehicle information.\n\n{0}", response.ReasonPhrase));
            return vehicleData;
        }

        public static async Task<DealersResponse> GetDealerDataAsync(DatasetIdResponse d, int dealerId)
        {
            HttpResponseMessage response = await client.GetAsync(client.BaseAddress.AbsoluteUri + string.Format("/api/{0}/dealers/{1}", d.datasetId, dealerId));
            DealersResponse dealerData = null;
            if (response.IsSuccessStatusCode)
            {
                dealerData = await response.Content.ReadAsAsync<DealersResponse>();
            }
            else throw new Exception(string.Format("Unabled to retrieve dealer information.\n\n{0}", response.ReasonPhrase));
            return dealerData;
        }

        public static async Task<AnswerResponse> PostAnswer(DatasetIdResponse d, Answer answerRequest)
        {
            var jsonContent = JsonConvert.SerializeObject(answerRequest);
            var stringContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(client.BaseAddress.AbsoluteUri + string.Format("/api/{0}/answer", d.datasetId), stringContent);
            AnswerResponse answerResponse = null;
            if (response.IsSuccessStatusCode)
            {
                answerResponse = await response.Content.ReadAsAsync<AnswerResponse>();
            }
            else throw new Exception(string.Format("Failed to post the answer.\n\n{0}\n\n{1}", response.ReasonPhrase, jsonContent));
            return answerResponse;
        }
    }
}
