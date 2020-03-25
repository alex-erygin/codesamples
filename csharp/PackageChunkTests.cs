using System.Collections.Generic;
using Atol.Api.Model;
using Atol.Api.Modules;
using NUnit.Framework;

namespace Atol.Api.Tests
{
    [TestFixture]
    public class PackageChunkTests : TestBase
    {
        /// <summary>
        /// Если на обработку чанка назначить исполнителей, то чанк получит статус Assigned и можно будет посмотреть на назначенцев.
        /// </summary>
        [Test]
        public void AssignUsers_ChunkStateIsAssigned()
        {
            var package = CreatePackageWithData(PackageProcessingPolicy.Default);
            var chunk = package.CreateChunk();

            var alice = new User();
            var bob = new User();
            chunk.Value.AssignUsers(alice, bob);
            Assert.AreEqual(ChunkStatus.Assigned, chunk.Value.Status);
            Assert.IsNotEmpty(chunk.Value.Assignees);
        }


        /// <summary>
        /// Один из двух назначенцев закончил работу, чанк в состоянии Active.
        /// </summary>
        [Test]
        public void CompleteChunk_ChunkWithTwoAssignees_CompleteByOnlyOneAssignee_ChunkStatusIsActive()
        {
            var package = CreatePackageWithData(PackageProcessingPolicy.Default);
            var chunk = package.CreateChunk();

            var alice = new User { Id = 1 };
            var bob = new User { Id = 2 };
            chunk.Value.AssignUsers(alice, bob);

            var processedChunk = new ProcessedChunk(chunk.Value.Id, alice, chunk.Value.ToProcessed( Mark.Correct ));
            var result = package.RegisterResult(processedChunk);
            Assert.True(result.IsSuccess);
            Assert.AreEqual(ChunkStatus.Active, chunk.Value.Status);
        }

        
        /// <summary>
        /// Статус свежесозданного чанка: NotStarted.
        /// </summary>
        [Test]
        public void CreateChunk_ChunkStatusIsNotStarted()
        {
            var package = CreatePackageWithData(PackageProcessingPolicy.Default);
            var chunk = package.CreateChunk();
            Assert.AreEqual( ChunkStatus.NotAssigned, chunk.Value.Status );
        }
        

        /// <summary>
        /// Попытка регистрации результата пользователем, на которого данная задача не была назначена.
        /// </summary>
        [Test]
        public void CompleteChunk_RegisterResultWithWrongUser_Failure()
        {
            var package = CreatePackageWithData(PackageProcessingPolicy.Default);
            var chunk = package.CreateChunk();
            var bob = new User { Id = 123123, Name = "Bob" };
            var result = package.RegisterResult(new ProcessedChunk(chunk.Value.Id, bob, new List<ProcessedItem>()));
            Assert.True(result.IsFailure);
        }

        /// <summary>
        /// Тест на обработку некорректных параметров.
        /// </summary>
        [Test]
        public void UnassingUser_BadArgsCheck()
        {
            var chunk = new PackageChunk();
            Assert.IsTrue(chunk.UnassignUser(null).IsFailure);
        }


        /// <summary>
        /// Удаление назначенного на чанк пользователя, который не завершил обработку чанка.
        /// </summary>
        [Test]
        public void UnassignUser_AssignedUser_UserUnassigned()
        {
            var package = CreatePackageWithData(PackageProcessingPolicy.Default);
            var chunk = package.CreateChunk();
            var bob = new User { Id = 1, Name = "Bob" };

            var result = chunk.Value.AssignUsers(bob);
            Assert.IsTrue(result.IsSuccess);

            result = chunk.Value.UnassignUser(bob);
            Assert.True(result.IsSuccess);
            Assert.IsEmpty(chunk.Value.Assignees);
        }

        /// <summary>
        /// Попытка удалить назначение пользователя, который не был ранее назначен.
        /// </summary>
        [Test]
        public void UnassignUser_UnassignedUser_Failure()
        {
            var package = CreatePackageWithData(PackageProcessingPolicy.Default);
            var chunk = package.CreateChunk();
            var alice = new User { Id = 2, Name = "Alice" };
            var result = chunk.Value.UnassignUser(alice);
            Assert.IsTrue(result.IsFailure);
        }

        // Попытка удаление пользователя, который уже зарегистрировал выполнение задачи.
        [Test]
        public void UnassignUser_UserWithRegisteredResult_Failure()
        {
            var package = CreatePackageWithData(PackageProcessingPolicy.Default);
            var chunk = package.CreateChunk();
            var alice = new User { Id = 2, Name = "Alice" };
            Assert.True(chunk.Value.AssignUsers(alice).IsSuccess);
            Assert.True(package.RegisterResult(new ProcessedChunk(chunk.Value.Id, alice, chunk.Value.ToProcessed( Mark.Correct ))).IsSuccess);
            Assert.True(chunk.Value.UnassignUser(alice).IsFailure);
        }

        /// <summary>
        /// Оба назначенца зарегистрировали результат работы, чанк должен быть помечен как completed.
        /// </summary>
        [Test]
        public void CompleteChunk_ChunkWithToAssignees_CompleteByAllAssignees_ChunkStatusIsCompleted()
        {
            var package = CreatePackageWithData(PackageProcessingPolicy.Default);
            var chunk = package.CreateChunk();

            var alice = new User { Id = 1 };
            var bob = new User { Id = 2 };
            chunk.Value.AssignUsers(alice, bob);

            var result1 = new ProcessedChunk( chunk.Value.Id, alice, chunk.Value.ToProcessed( Mark.Correct ) );
            var result2 = new ProcessedChunk( chunk.Value.Id, bob, chunk.Value.ToProcessed( Mark.Correct ) );

            var result = package.RegisterResult(result1);
            Assert.True(result.IsSuccess);
            result = package.RegisterResult(result2);
            Assert.True(result.IsSuccess);

            Assert.AreEqual(ChunkStatus.Completed, chunk.Value.Status);
        }

        /// <summary>
        /// Нельзя зарегистрировать один и тот же результат работы повторно.
        /// </summary>
        [Test]
        public void CompleteChunk_CompleteSameChunkTwoTimes_ReturnsFailure()
        {
            var package = CreatePackageWithData(PackageProcessingPolicy.Default);
            var chunk = package.CreateChunk();
            var alice = new User();
            chunk.Value.AssignUsers(alice);
            var processedChunk = new ProcessedChunk(chunk.Value.Id, alice, chunk.Value.ToProcessed( Mark.Correct ));

            var result = package.RegisterResult(processedChunk);
            Assert.True(result.IsSuccess);

            result = package.RegisterResult(processedChunk);
            Assert.False(result.IsSuccess);
        }


        /// <summary>
        /// Попытка зарегистрировать результат с другим чанком, не тем, который был обработан. 
        /// </summary>
        [Test]
        public void CompleteTask_CompleteResultWithAnotherChunk_ReturnsFailure()
        {
            var package = CreatePackageWithData(PackageProcessingPolicy.Default);
            var originalChunk = package.CreateChunk();
            var anotherChunk = package.CreateChunk();
            
            var alice = new User();
            originalChunk.Value.AssignUsers(alice);

            var processedChunk = new ProcessedChunk(anotherChunk.Value.Id, alice, new List<ProcessedItem>());
            var result = package.RegisterResult(processedChunk);
            Assert.True(result.IsFailure);
        }


        /// <summary>
        /// Попытка зарегистрировать результат, в котором нет обработанных элементов. Должен зафэйлиться.
        /// </summary>
        [Test]
        public void CompleteTask_ProcessingResultWithEmptyList_Failure()
        {
            var package = CreatePackageWithData(PackageProcessingPolicy.Default);
            var chunk = package.CreateChunk();
            var alice = new User();
            var processedChunk = new ProcessedChunk(chunk.Value.Id, alice, new List<ProcessedItem>());

            Assert.IsFalse(package.RegisterResult(processedChunk).IsSuccess);
        }

    }
}