﻿using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KafkaApp1.Service
{
    public class Producer
    {
        static void Produce(string topic, ClientConfig config)
        {
            using (var producer = new ProducerBuilder<string, string>(config).Build())
            {
                int numProduced = 0;
                int numMessages = 10;
                for (int i = 0; i < numMessages; ++i)
                {
                    var key = "alice";
                    var val = JObject.FromObject(new { count = i }).ToString(Formatting.None);

                    Console.WriteLine($"Producing record: {key} {val}");

                    producer.Produce(topic, new Message<string, string> { Key = key, Value = val },
                        (deliveryReport) =>
                        {
                            if (deliveryReport.Error.Code != ErrorCode.NoError)
                            {
                                Console.WriteLine($"Failed to deliver message: {deliveryReport.Error.Reason}");
                            }
                            else
                            {
                                Console.WriteLine($"Produced message to: {deliveryReport.TopicPartitionOffset}");
                                numProduced += 1;
                            }
                        });
                }

                producer.Flush(TimeSpan.FromSeconds(10));

                Console.WriteLine($"{numProduced} messages were produced to topic {topic}");
            }
        }

        static async Task<ClientConfig> LoadConfig(string configPath, string certDir)
        {
            try
            {
                var cloudConfig = (await File.ReadAllLinesAsync(configPath))
                    .Where(line => !line.StartsWith("#"))
                    .ToDictionary(
                        line => line.Substring(0, line.IndexOf('=')),
                        line => line.Substring(line.IndexOf('=') + 1));

                var clientConfig = new ClientConfig(cloudConfig);

                if (certDir != null)
                {
                    clientConfig.SslCaLocation = certDir;
                }

                return clientConfig;
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occured reading the config file from '{configPath}': {e.Message}");
                System.Environment.Exit(1);
                return null; // avoid not-all-paths-return-value compiler error.
            }
        }

        static async Task CreateTopicMaybe(string name, int numPartitions, short replicationFactor, ClientConfig cloudConfig)
        {
            using (var adminClient = new AdminClientBuilder(cloudConfig).Build())
            {
                try
                {
                    await adminClient.CreateTopicsAsync(new List<TopicSpecification> {
                        new TopicSpecification { Name = name, NumPartitions = numPartitions, ReplicationFactor = replicationFactor } });
                }
                catch (CreateTopicsException e)
                {
                    if (e.Results[0].Error.Code != ErrorCode.TopicAlreadyExists)
                    {
                        Console.WriteLine($"An error occured creating topic {name}: {e.Results[0].Error.Reason}");
                    }
                    else
                    {
                        Console.WriteLine("Topic already exists");
                    }
                }
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("usage: .. produce|consume <topic> <configPath> [<certDir>]");
            System.Environment.Exit(1);
        }

        public async Task RunAsync()
        {
            string[] args = new string[] { };
            var mode = "produce";
            var topic = "test1";
            var configPath = "librdkafka.config";
            var certDir = args.Length == 4 ? args[3] : null;

            var config = await LoadConfig(configPath, certDir);

            switch (mode)
            {
                case "produce":
                    await CreateTopicMaybe(topic, 1, 3, config);
                    Produce(topic, config);
                    break;
                default:
                    PrintUsage();
                    break;
            }
        }
    }
}
