using DataSetChallenge.ApiClient;
using DataSetChallenge.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataSetChallenge
{
    class Program
    {
        static void Main(string[] args)
        {
            RunAsync().GetAwaiter().GetResult();
        }

        static async Task RunAsync()
        {
            try
            {
                VAutoClient.Initialize();

                Console.WriteLine("Retreiving data set id...");
                DatasetIdResponse datasetIdResponse = await VAutoClient.GetDataSetIdAsync();
                string dataSetId = datasetIdResponse.datasetId;
                Console.WriteLine("Done.");

                Console.WriteLine("Retreiving vehicle ids...");
                VehicleIdsResponse vehicleIds = await VAutoClient.GetVehicleIdsAsync(dataSetId);
                Console.WriteLine("Done.");

                Console.WriteLine("Retreiving vehicle data...");
                IEnumerable<int> vehicleIdList = vehicleIds.vehicleIds.Distinct();   //ensure duplicate calls for vehicle data are not performed.
                Console.WriteLine("Done.");

                Console.WriteLine("Retreiving dealer data...");
                List<Task<VehicleResponse>> vehicleDataTasks = new List<Task<VehicleResponse>>();
                List<Task<DealersResponse>> dealerDataTasks = new List<Task<DealersResponse>>();
                await GetVehicleAndDealerData(dataSetId, vehicleIdList, vehicleDataTasks, dealerDataTasks);
                Console.WriteLine("Done.");

                Console.WriteLine("Building answer...");
                Answer answer = BuildAnswer(dealerDataTasks, vehicleDataTasks);
                Console.WriteLine("Done.");

                Console.WriteLine("Posting answer...");
                AnswerResponse answerResponse = await VAutoClient.PostAnswer(dataSetId, answer);
                Console.WriteLine("Done.");

                Console.WriteLine("Result: {0}\nMessage: {1}\nMilliseconds: {2}", answerResponse.success, answerResponse.message, answerResponse.totalMilliseconds);
            }
            catch (Exception e)
            {
                Console.WriteLine("\n\nException: {0}\n\nStackTrace: {1}", e.Message, e.StackTrace);
            }
        }

        private static async Task GetVehicleAndDealerData(string dataSetId, IEnumerable<int> vehicleIdList, List<Task<VehicleResponse>> vehicleDataTasks, List<Task<DealersResponse>> dealerDataTasks)
        {
            HashSet<int> dealerHashSet = new HashSet<int>();
            object o = new object();
            foreach (var id in vehicleIdList)
            {
                //Retrireve vehicle information - which provides access to the dealer id.
                Task<VehicleResponse> vehicleTask = VAutoClient.GetVehicleDataAsync(dataSetId, id);

                //cache reference to the task to be awaited later
                vehicleDataTasks.Add(vehicleTask);

                /*
                 * The use of continuation tasks is an attempt to avoid the opportuntity cost of waiting for the all the vehicle info calls to complete before making any dealer data calls.
                 * A single call for dealer data does not require that all calls for vehicle data have been completed - thus it is a lost opportunity to progress if we just wait for them to complete.
                 * A hashset is used for bookeeping so that multiple calls for the same deal data are not performed.  Lookups should be fast, O(1), and scalable at the expense of storage for dealer ids "in use."
                 */
#pragma warning disable 4014
                //given calls for vehicle information have already completed - "continue" to retrieve the dealer data immediately rather than wait for all vehicle info callouts to complete.
                vehicleTask.ContinueWith((antecedent) =>
                {
                    bool isDealerBeingHandled = false;

                    //Unfortunately .NET does not have a built in thread-safe hashset so we must perform synchronization on our own.
                    lock (o)
                    {
                        isDealerBeingHandled = dealerHashSet.Contains(antecedent.Result.dealerId);
                        if (isDealerBeingHandled == false)
                        {
                            dealerHashSet.Add(antecedent.Result.dealerId);
                        }
                    }

                    //Do not call out for dealer data if another thread is already tasked with that responsibility
                    if (isDealerBeingHandled == false)
                    {
                        //Retrieve dealer data 
                        Task<DealersResponse> dealerTask = VAutoClient.GetDealerDataAsync(dataSetId, antecedent.Result.dealerId);

                        //cache reference to the task to be awaited later
                        dealerDataTasks.Add(dealerTask);
                    }
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
#pragma warning restore 4014
            }

            await Task.WhenAll(vehicleDataTasks); //wait for vehicle data calls to complete to enusre dealer data calls are created.
            await Task.WhenAll(dealerDataTasks);  //Given all vehcicle data calls are complete we can be sure all deal data tasks have been created with the hope the some has already started.
        }

        public static Answer BuildAnswer(List<Task<DealersResponse>> dealerDataTasks, List<Task<VehicleResponse>> vehicleDataTasks)
        {
            Answer answer = new Answer() { dealers = new DealerAnswer[dealerDataTasks.Count] };
            for (int i = 0; i < dealerDataTasks.Count; ++i)
            {
                var result = dealerDataTasks[i].Result;
                answer.dealers[i] = new DealerAnswer() { dealerId = result.dealerId, name = result.name, vehicles = new List<VehicleAnswer>() };
            }

            for (int i = 0; i < vehicleDataTasks.Count; ++i)
            {
                var vehicleResult = vehicleDataTasks[i].Result;
                DealerAnswer dealerResult = null;
                for (int j = 0; j < answer.dealers.Length; ++j)
                {
                    dealerResult = answer.dealers[j];
                    if (dealerResult.dealerId == vehicleResult.dealerId) break;
                }

                dealerResult.vehicles.Add(new VehicleAnswer()
                {
                    vehicleId = vehicleResult.vehicleId,
                    year = vehicleResult.year,
                    make = vehicleResult.make,
                    model = vehicleResult.model
                });
            }

            return answer;
        }
    }
}
