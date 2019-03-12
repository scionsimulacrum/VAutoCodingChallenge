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
                DatasetIdResponse dataSet = await VAutoClient.GetDataSetIdAsync();
                Console.WriteLine("Done.");

                Console.WriteLine("Retreiving vehicle ids...");
                VehicleIdsResponse vehicleIds = await VAutoClient.GetVehicleIdsAsync(dataSet);
                Console.WriteLine("Done.");

                Console.WriteLine("Retreiving vehicle data...");
                IEnumerable<int> vehicleIdList = vehicleIds.vehicleIds.Distinct();   //ensure duplicate calls for vehicle data are not performed.
                List<Task<VehicleResponse>> vehicleDataTasks = new List<Task<VehicleResponse>>();
                Console.WriteLine("Done.");

                Console.WriteLine("Retreiving dealer data...");
                List<Task<DealersResponse>> dealerDataTasks = await GetDealerData(dataSet, vehicleIdList, vehicleDataTasks);
                Console.WriteLine("Done.");

                Console.WriteLine("Building answer...");
                Answer answerRequest = BuildAnswerRequest(dealerDataTasks, vehicleDataTasks);
                Console.WriteLine("Done.");

                Console.WriteLine("Posting answer...");
                AnswerResponse answerResponse = await VAutoClient.PostAnswer(dataSet, answerRequest);
                Console.WriteLine("Done.");

                Console.WriteLine("Result: {0}\nMessage: {1}\nMilliseconds: {2}", answerResponse.success, answerResponse.message, answerResponse.totalMilliseconds);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static async Task<List<Task<DealersResponse>>> GetDealerData(DatasetIdResponse dataSet, IEnumerable<int> vehicleIdList, List<Task<VehicleResponse>> vehicleInfoTaskList)
        {
            HashSet<int> dealerHashSet = new HashSet<int>();
            List<Task<DealersResponse>> dealerDataTaskList = new List<Task<DealersResponse>>();
            object o = new object();
            foreach (var id in vehicleIdList)
            {
                //Retrireve vehicle information - which provides access to the dealer id.
                Task<VehicleResponse> vehicleTask = VAutoClient.GetVehicleDataAsync(dataSet, id);

                //cache reference to the task to be awaited later
                vehicleInfoTaskList.Add(vehicleTask);

#pragma warning disable 4014
                //for calls for vehicle information that have already completed - "continue" to retrieve the dealer data immediately rather than wait for all vehicle info callouts to complete.
                vehicleTask.ContinueWith((antecedent) =>
                {
                    bool isDealerBeingHandled = false;

                    //Unfortunately .NET does not have a built in thread-safe hashset so we must perform synchronization on our own.
                    lock (o)
                    {
                        isDealerBeingHandled = dealerHashSet.Contains(antecedent.Result.dealerId);
                    }

                    //Do not call out for deal data if another thread is already tasked with that responsibility
                    if (isDealerBeingHandled == false)
                    {
                        //Unfortunately .NET does not have a built in thread-safe hashset so we must perform synchronization on our own.
                        lock (o)
                        {
                            dealerHashSet.Add(antecedent.Result.dealerId);
                        }

                        //Retrieve dealer data 
                        Task<DealersResponse> dealerTask = VAutoClient.GetDealerDataAsync(dataSet, antecedent.Result.dealerId);

                        //cache reference to the task to be awaited later
                        dealerDataTaskList.Add(dealerTask);
                    }
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
#pragma warning restore 4014
            }

            await Task.WhenAll(vehicleInfoTaskList); //wait for vehicle data calls to complete to enusre dealer data calls are created.
            await Task.WhenAll(dealerDataTaskList);  //Given all vehcicle data calls are complete we can be sure all deal data tasks have been created with the hope the some has already started.

            /*
             * The use of continuation tasks is an attempt to avoid the opportuntity cost of waiting for the all the vehicle info calls to complete before making any dealer data calls.
             * A single call for dealer data does not require that all calls for vehicle data have been completed - thus it is a lost opportunity to progress if we just wait for them to complete.
             * A hashset is used for bookeeping so that multiple calls for the same deal data are not performed.  Lookups should be fast, O(1), and scalable at the expense of storage for dealer ids "in use."
             */

            return dealerDataTaskList;
        }

        public static Answer BuildAnswerRequest(List<Task<DealersResponse>> dealerDataTasks, List<Task<VehicleResponse>> vehicleDataTasks)
        {
            Answer answerRequest = new Answer() { dealers = new DealerAnswer[dealerDataTasks.Count] };
            for (int i = 0; i < dealerDataTasks.Count; ++i)
            {
                var result = dealerDataTasks[i].Result;
                answerRequest.dealers[i] = new DealerAnswer() { dealerId = result.dealerId, name = result.name, vehicles = new List<VehicleAnswer>() };
            }

            //The set of dealers should be much smaller than the set of vehicles
            for (int i = 0; i < vehicleDataTasks.Count; ++i)
            {
                var vehicleResult = vehicleDataTasks[i].Result;
                DealerAnswer dealerResult = null;
                for (int j = 0; j < answerRequest.dealers.Length; ++j)
                {
                    dealerResult = answerRequest.dealers[j];
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

            return answerRequest;
        }
    }
}
