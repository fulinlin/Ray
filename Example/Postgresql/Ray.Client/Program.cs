﻿using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Ray.Handler;
using Ray.IGrains;
using Ray.IGrains.Actors;
using Ray.RabbitMQ;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Ray.Core.Message;
using System.Diagnostics;
using Ray.Core.MQ;
using Ray.Core;

namespace Ray.Client
{
    class Program
    {
        static int Main(string[] args)
        {
            return RunMainAsync().Result;
        }

        private static async Task<int> RunMainAsync()
        {
            try
            {
                using (var client = await StartClientWithRetries())
                {
                    var manager = client.ServiceProvider.GetService<ISubManager>();
                    await manager.Start(new[] { "Core", "Read", "Rep" });
                    //Console.WriteLine("Press Enter to actor count");
                    var actorAcount = 20;
                    IAccount[] actors = new IAccount[actorAcount];
                    for (int i = 0; i < actorAcount; i++)
                    {
                        actors[i] = client.GetGrain<IAccount>(i);
                    }
                    while (true)
                    {
                        Console.WriteLine("Press Enter to terminate...");
                        var length = int.Parse(Console.ReadLine());

                        var stopWatch = new Stopwatch();
                        stopWatch.Start();
                        var tasks = new Task[length * actorAcount];
                        Parallel.For(0, length, i =>
                        {
                            var transfer = false;
                            for (int x = 0; x < actorAcount; x++)
                            {
                                if (!transfer)
                                {
                                    tasks[i * actorAcount + x] = actors[x].AddAmount(1000);
                                    transfer = true;
                                }
                                else
                                {
                                    tasks[i * actorAcount + x] = actors[x - 1].Transfer(x, 500);
                                    transfer = false;
                                }

                            }
                        });
                        await Task.WhenAll(tasks);
                        stopWatch.Stop();
                        Console.WriteLine($"{length * actorAcount}次操作完成，耗时:{stopWatch.ElapsedMilliseconds}ms");
                        await Task.Delay(200);
                        for (int i = 0; i < actorAcount; i++)
                        {
                            Console.WriteLine($"End:{i}的余额为{await actors[i].GetBalance()}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 1;
            }
        }

        private static async Task<IClusterClient> StartClientWithRetries(int initializeAttemptsBeforeFailing = 5)
        {
            int attempt = 0;
            IClusterClient client;
            while (true)
            {
                try
                {
                    client = new ClientBuilder()
                     .UseLocalhostClustering()
                    .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(IAccount).Assembly).WithReferences())
                    .ConfigureLogging(logging => logging.AddConsole())
                    .ConfigureServices((servicecollection) =>
                    {
                        SubManager.Parse(servicecollection, typeof(AccountCoreHandler).Assembly);//注册handle
                        servicecollection.AddSingleton<IOrleansClientFactory, OrleansClientFactory>();//注册Client获取方法
                        servicecollection.AddSingleton<ISerializer, ProtobufSerializer>();//注册序列化组件
                        servicecollection.AddRabbitMQ<MessageInfo>();//注册RabbitMq为默认消息队列
                        servicecollection.PostConfigure<RabbitConfig>(c =>
                    {
                        c.UserName = "admin";
                        c.Password = "admin";
                        c.Hosts = new[] { "192.168.125.230:5672" };
                        c.MaxPoolSize = 100;
                        c.VirtualHost = "test";
                    });
                    })
                    .Build();

                    await client.ConnectAndFill();
                    Console.WriteLine("Client successfully connect to silo host");
                    break;
                }
                catch (SiloUnavailableException)
                {
                    attempt++;
                    Console.WriteLine($"Attempt {attempt} of {initializeAttemptsBeforeFailing} failed to initialize the Orleans client.");
                    if (attempt > initializeAttemptsBeforeFailing)
                    {
                        throw;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(4));
                }
            }

            return client;
        }
    }
}