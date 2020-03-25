using System;
using System.IO;
using System.Threading;
using Atol.Api.Common;
using Atol.Api.Model;
using Atol.Api.Modules;
using CSharpFunctionalExtensions;
using Moq;
using NUnit.Framework;

namespace Atol.Api.Tests.Integration
{
    /// <summary>
    /// Интеграционные тесты.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class IntegrationTests : TestBase
    {
        /// <summary>
        /// Сценарий 1. Два пользователя обрабатывают один пакет.
        /// ┌─────┬───────────────────────────────────────────────────────────┬───────────┬────────────────────────────────────────────────────────────┐
        /// │ Шаг │                    Состояние/Действие                     │ Результат │                          Описание                          │
        /// ├─────┼───────────────────────────────────────────────────────────┼───────────┼────────────────────────────────────────────────────────────┤
        /// │   - │ В системе один пакет (из 10 штук), два юзера: Джей и Боб  │           │                                                            │
        /// │   1 │ Джей получает задачу из текущего пакета                   │ Успех     │                                                            │
        /// │   2 │ Джей обрабатывает задачу и регистрирует результат         │ Успех     │                                                            │
        /// │   3 │ Джей просит еще одну задачу                               │ Отказ     │ В этом пакете Джей уже все обработал, а других пакетов нет │
        /// │   - │ В системе Открытых пакетов: 1, Закрытых: 0                │           │                                                            │
        /// │   4 │ Боб получает задачу из текущего пакета                    │ Успех     │                                                            │
        /// │   5 │ Боб выполняет задачу и регистрирует результат             │ Успех     │                                                            │
        /// │   - │ В системе 1 закрытый пакет и 0 открытых                   │           │ Пакет успешно обработан всеми исполнителями                │
        /// └─────┴───────────────────────────────────────────────────────────┴───────────┴────────────────────────────────────────────────────────────┘
        /// </summary>
        [Test]
        public void ProcessingScenario1()
        {
            var policy = PackageProcessingPolicy.Default;
            policy.TaskSize = 10;

            var package = CreatePackageWithData(policy.TaskSize, policy);
            var metadataRepository = new InMemoryPackageMetadataRepository();
            metadataRepository.Reset();
            
            var repository = new PackageRepository(new JsonFileStorage(BaseDirectory), metadataRepository);
            var taskManager = new TaskManager(repository, metadataRepository);
            var packageManager = new PackageManager(repository, metadataRepository);
            Assert.True( repository.SavePackage(package).IsSuccess );

            var jey = new User { Id = 1, Name = "Джей" };
            var bob = new User { Id = 2, Name = "Боб" };

            Check( "В системе один пакет (из 10 штук), два юзера: Джей и Боб", () => {
                var statistics = packageManager.GetStatistics();
                Assert.AreEqual( 1, statistics.Value.OpenedPackages );
            } );

            Result<PackageChunk> jeyTask = new Result<PackageChunk>();
            Step( "Джей получает задачу из текущего пакета", () => 
            {
                jeyTask = taskManager.GetCurrentTask( jey );
                Assert.True( jeyTask.IsSuccess );
            } );

            Step("Джей обрабатывает задачу и регистрирует результат", () =>
            {
                Assert.NotNull( package = repository.GetById( package.Id ).Value );
                Assert.True( package.RegisterResult(new ProcessedChunk(jeyTask.Value.Id, jey, jeyTask.Value.ToProcessed(Mark.Correct))).IsSuccess );
                Assert.True( repository.SavePackage(package).IsSuccess );
            });

            Step("Джей просит еще одну задачу. Получает отказ.", () =>
            {
                jeyTask = taskManager.GetCurrentTask(jey);
                Assert.True(jeyTask.IsFailure);
            });

            Check("В системе Открытых пакетов: 1, Закрытых: 0 ", () =>
            {
                var statistics = packageManager.GetStatistics();
                Assert.AreEqual(1, statistics.Value.OpenedPackages);
            });

            Result<PackageChunk> bobTask = new Result<PackageChunk>();
            Step("Боб получает задачу из текущего пакета", () =>
            {
                bobTask = taskManager.GetCurrentTask(bob);
                Assert.True(bobTask.IsSuccess);
            });

            Step("Боб выполняет задачу и регистрирует результат", () =>
            {
                Assert.NotNull(package = repository.GetById(package.Id).Value);
                Assert.True(package.RegisterResult(new ProcessedChunk(bobTask.Value.Id, bob, bobTask.Value.ToProcessed(Mark.Correct))).IsSuccess);
                Assert.True(repository.SavePackage(package).IsSuccess);
            });

            Check( "В системе 1 закрытый пакет и 0 открытых", () =>
            {
                var statistics = packageManager.GetStatistics();
                Assert.AreEqual(1, statistics.Value.ClosedPackages);
                Assert.AreEqual(0, statistics.Value.OpenedPackages);
            });
        }


        /// <summary>
        /// Сценарий 2: отброс бездельника и успешное завершение пакета другим пользователем.
        /// ┌─────┬────────────────────────────────────────────────────────────────────┬───────────┬──────────────────────────────────────────────────┐
        /// │ Шаг │                         Состояние/Действие                         │ Результат │                     Описание                     │
        /// ├─────┼────────────────────────────────────────────────────────────────────┼───────────┼──────────────────────────────────────────────────┤
        /// │   - │ В системе один пакет (из 10 штук), три юзера: Иван, Федор и Корней │           │                                                  │
        /// │   1 │ Иван получает задачу из текущего пакета                            │ Успех     │                                                  │
        /// │   2 │ Иван обрабатывает задачу и регистрирует результат                  │ Успех     │                                                  │
        /// │   - │ В системе Открытых пакетов: 1, Закрытых: 0                         │           │                                                  │
        /// │   4 │ Федор получает задачу из текущего пакета                           │ Успех     │                                                  │
        /// │   5 │ Федор не успевает выполнить задачу в срок                          │ Успех     │                                                  │
        /// │   6 │ Корней просит задачу из текущего пакета                            │ Успех     │ При поиске нашли опоздуна Федора и отбросили его │
        /// │   7 │ Корней выполняет задачу и регистрирует результат                   │ Успех     │                                                  │
        /// │   8 │ Федор пытается зарегистрировать результат                          │ Неудача   │ Задача уже уплыла                                │
        /// │   9 │ Федор попытался получить другую задачу                             │ Неудача   │ Задач больше нет, пакет обработан                │
        /// │   - │ В системе 1 закрытый пакет и 0 открытых                            │           │ Пакет успешно обработан всеми исполнителями      │
        /// └─────┴────────────────────────────────────────────────────────────────────┴───────────┴──────────────────────────────────────────────────┘
        /// </summary>
        [Test]
        public void ProcessingScenario2()
        {
            var policy = new PackageProcessingPolicy
            {
                ExpirationTimeout = TimeSpan.FromMilliseconds( 1 ),
                GroupSize = 2,
                TaskSize = 10
            };
            var package = CreatePackageWithData(policy.TaskSize, policy);

            var metadataRepository = new InMemoryPackageMetadataRepository();
            metadataRepository.Reset();
            
            var repository = new PackageRepository(new JsonFileStorage(BaseDirectory), metadataRepository);
            var taskManager = new TaskManager(repository, metadataRepository);
            var packageManager = new PackageManager(repository, metadataRepository);
            
            Assert.IsTrue(repository.SavePackage(package).IsSuccess);

            var ivan = new User { Id = 1, Name = "Иван" };
            var fedor = new User { Id = 2, Name = "Федор" };
            var korney = new User {Id = 3, Name = "Корней" };

            Check("В системе один пакет (из 10 штук), три юзера: Иван, Федор и Корней", () =>
            {
                var statistics = packageManager.GetStatistics();
                Assert.AreEqual(1, statistics.Value.OpenedPackages);
            });

            Result<PackageChunk> ivanTask = new Result<PackageChunk>();
            Step("Иван получает задачу из текущего пакета", () =>
            {
                ivanTask = taskManager.GetCurrentTask(ivan);
                Assert.True(ivanTask.IsSuccess);

                package = repository.GetById( ivanTask.Value.PackId ).Value;
                Assert.AreEqual(1, package.Chunks.Count);
                Assert.True(package.Chunks[0].Assignees.Contains(ivan.Id));
            });

            Step("Иван обрабатывает задачу и регистрирует результат", () =>
            {
                Assert.True(package.RegisterResult(new ProcessedChunk(ivanTask.Value.Id, ivan, ivanTask.Value.ToProcessed(Mark.Correct))).IsSuccess);
                Assert.True(repository.SavePackage(package).IsSuccess);
            });

            Check("В системе Открытых пакетов: 1, Закрытых: 0", () =>
            {
                var statistics = packageManager.GetStatistics();
                Assert.AreEqual(1, statistics.Value.OpenedPackages);
            });

            Result<PackageChunk> fedorTask = new Result<PackageChunk>();
            Step("Федор получает задачу из текущего пакета", () =>
            {
                fedorTask = taskManager.GetCurrentTask(fedor);
                Assert.True(fedorTask.IsSuccess);
                package = repository.GetCurrentPackage().Value;
                Assert.AreEqual(1, package.Chunks.Count);
                Assert.True(package.Chunks[0].Assignees.Contains(fedor.Id));
            });

            Step( "Федор не успевает выполнить задачу в срок", () =>
            {
                Thread.Sleep(policy.ExpirationTimeout);
            } );

            Result<PackageChunk> korneyTask = new Result<PackageChunk>();
            Step( "Корней просит выдать задачу и получает ее", () =>
            {
                korneyTask = taskManager.GetCurrentTask(korney);
                Assert.True(korneyTask.IsSuccess);
            });

            Step( "Корней выполняет задачу и регистрирует результат", () =>
            {
                package = repository.GetById(korneyTask.Value.PackId).Value;
                Assert.True(package.RegisterResult(new ProcessedChunk(korneyTask.Value.Id, korney, korneyTask.Value.ToProcessed(Mark.Incorrect))).IsSuccess);
                Assert.IsTrue(repository.SavePackage(package).IsSuccess);
            });

            Step("Федор пытается зарегистрировать результат, не выходит", () =>
            {
                var result = package.RegisterResult(
                    new ProcessedChunk(fedorTask.Value.Id, fedor, fedorTask.Value.ToProcessed(Mark.Correct)));
                Assert.True( result.IsFailure );
            });

            Check("В системе Открытых пакетов: 0, Закрытых: 1", () =>
            {
                var statistics = packageManager.GetStatistics();
                Assert.AreEqual(1, statistics.Value.ClosedPackages);
                Assert.AreEqual(0, statistics.Value.OpenedPackages);
            });
        }
        
        
        /// <summary>
        /// Тест на неблокирущую обработку. Когда в текущем пакете свободных чанков нет, а в следующем есть.
        /// </summary>
        /* +-----+-------------------------------------------------------------+-----------+----------+ */
        /* | Шаг |                          Действие                           | Результат | Описание | */
        /* +-----+-------------------------------------------------------------+-----------+----------+ */
        /* |   - | В системе 2 пакета и три пользователя: Иван, Федор и Корней |           |          | */
        /* |   1 | Иван получает задачу                                        | Успех     |          | */
        /* |   2 | Иван обрабатывает его и регистрирует результат              | Успех     |          | */
        /* |   3 | Федор получает задачу                                       | Успех     |          | */
        /* |   - | Задачи Ивана и Федора взяты из одного пакета                |           |          | */
        /* |   5 | Никифор получает задачу                                     | Успех     |          | */
        /* |   - | Задача Никифора из другого пакета                           |           |          | */
        /* |   8 | Федор регистрирует результат выполнения задачи              | Успех     |          | */
        /* |   9 | Никифор регистрирует результат выполнения задачи            | Успех     |          | */
        /* |   - | Состояние системы: 1 закрытый пакет, 1 открытый             |           |          | */
        /* |  10 | Никифор пытается получить задачу                            | Неудача   |          | */
        /* |  11 | Иван пытается получить задачу                               | Успех     |          | */
        /* |  12 | Федор пытается получить задачу                              | Неудача   |          | */
        /* |  13 | Иван регистрирует результат выполнения задачи               | Успех     |          | */
        /* |   - | Состояние системы: 2 закрытых пакета, 0 открытых            |           |          | */
        /* +-----+-------------------------------------------------------------+-----------+----------+ */
        [Test]
        public void NonBlockingProcessingTest()
        {
            var policy = new PackageProcessingPolicy
            {
                ExpirationTimeout = TimeSpan.FromHours( 1 ),
                GroupSize = 2,
                TaskSize = 10
            };

            var metadataRepository = new InMemoryPackageMetadataRepository();
            metadataRepository.Reset();
            var repository = new PackageRepository(new JsonFileStorage(BaseDirectory), metadataRepository);

            var taskManager = new TaskManager(repository,metadataRepository);
            var packageManager = new PackageManager(repository, metadataRepository);

            var firstPackage = CreatePackageWithData(policy.TaskSize, policy);
            var secondPackage = CreatePackageWithData(policy.TaskSize, policy);
            
            Assert.IsTrue(repository.SavePackage(firstPackage).IsSuccess);
            Assert.IsTrue(repository.SavePackage(secondPackage).IsSuccess);

            var ivan = new User {Id = 1, Name = "Иван"};
            var fedor = new User {Id = 2, Name = "Федор"};
            var nikifor = new User {Id = 3, Name = "Никифор"};

            Check("В системе два пакета (из 10 штук), три юзера: Иван, Федор и Никифор", () =>
            {
                var statistics = packageManager.GetStatistics();
                Assert.AreEqual(2, statistics.Value.OpenedPackages);
            });

            var package = packageManager.GetCurrentPackage().Value;
            
            Result<PackageChunk> ivanTask = new Result<PackageChunk>();
            Step("Иван получает задачу", () =>
            {
                ivanTask = taskManager.GetCurrentTask(ivan);
                Assert.True(ivanTask.IsSuccess);

                package = repository.GetById( ivanTask.Value.PackId ).Value;
                Assert.AreEqual(1, package.Chunks.Count);
                Assert.True(package.Chunks[0].Assignees.Contains(ivan.Id));
            });
            
            Step("Иван обрабатывает задачу и регистрирует результат", () =>
            {
                Assert.True(package.RegisterResult(new ProcessedChunk(ivanTask.Value.Id, ivan, ivanTask.Value.ToProcessed(Mark.Correct))).IsSuccess);
                Assert.True(repository.SavePackage(package).IsSuccess);
            });
            
            Check("В системе Открытых пакетов: 2, Закрытых: 0", () =>
            {
                var statistics = packageManager.GetStatistics();
                Assert.AreEqual(2, statistics.Value.OpenedPackages);
            });
            
            Result<PackageChunk> fedorTask = new Result<PackageChunk>();
            Step( "Федор получает задачу", () =>
            {
                fedorTask = taskManager.GetCurrentTask( fedor );
                Assert.True( fedorTask.IsSuccess );
            });
            
            Check( "Задачи Ивана и Федора взяты из одного пакета", () =>
            {
                Assert.AreEqual( ivanTask.Value.PackId, fedorTask.Value.PackId );
            });

            Result<PackageChunk> nikiforTask = new Result<PackageChunk>();
            Step( "Никифор получает задачу", () =>
            {
                nikiforTask = taskManager.GetCurrentTask( nikifor );
                Assert.True( nikiforTask.IsSuccess );
            } );
            
            Check( "Задача Никифора из другого пакета", () =>
            {
                Assert.AreNotEqual( ivanTask.Value.PackId, nikiforTask.Value.PackId );
            } );
            
            Step( "Федор регистрирует результат выполнения задачи", () =>
            {
                package = repository.GetById(fedorTask.Value.PackId).Value;
                Assert.True(package.RegisterResult(new ProcessedChunk(fedorTask.Value.Id, fedor, fedorTask.Value.ToProcessed(Mark.Incorrect))).IsSuccess);
                Assert.True(repository.SavePackage(package).IsSuccess);
            });
            
            Step( "Никифор регистрирует результат выполнения задачи", () =>
            {
                package = repository.GetById(nikiforTask.Value.PackId).Value;
                Assert.True(package.RegisterResult(new ProcessedChunk(nikiforTask.Value.Id, nikifor, nikiforTask.Value.ToProcessed(Mark.Incorrect))).IsSuccess);
                Assert.True(repository.SavePackage(package).IsSuccess);
            });
            
            Check("В системе Открытых пакетов: 1, Закрытых: 1", () =>
            {
                var statistics = packageManager.GetStatistics();
                Assert.AreEqual(1, statistics.Value.OpenedPackages);
                Assert.AreEqual(1, statistics.Value.ClosedPackages);
            });
            
            Step("Никифор пытается получить задачу (не выйдет)", () =>
            {
                var nikiforTask2 = taskManager.GetCurrentTask(nikifor);
                Assert.True(nikiforTask2.IsFailure);
            });

            Result<PackageChunk> ivanTask2 = new Result<PackageChunk>();
            Step("Иван пытается получить задачу", () =>
            {
                ivanTask2 = taskManager.GetCurrentTask(ivan);
                Assert.True(ivanTask2.IsSuccess);
            });
            
            Step("Федор пытается получить задачу (не выйдет)", () =>
            {
                Assert.True(taskManager.GetCurrentTask(fedor).IsFailure);
            });
            
            Step( "Иван регистрирует результат выполнения задачи", () =>
            {
                package = repository.GetById(ivanTask2.Value.PackId).Value;
                Assert.True(package.RegisterResult(new ProcessedChunk(ivanTask2.Value.Id, ivan, ivanTask2.Value.ToProcessed(Mark.Incorrect))).IsSuccess);
                Assert.True(repository.SavePackage(package).IsSuccess);
            });
            
            Check( "Состояние системы: 2 закрытых пакета, 0 открытых", () =>
            {
                var statistics = packageManager.GetStatistics();
                Assert.AreEqual(0, statistics.Value.OpenedPackages);
                Assert.AreEqual(2, statistics.Value.ClosedPackages);
            } );
        }
    }
}