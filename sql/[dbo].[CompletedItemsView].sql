create view [dbo].[CompletedItemsView]
as
select 
	[user].[Id],
	[user].[Name],
	[completedLast10days] = count(record.TaskId)
from [dbo].[Users] [user]
	join [dbo].[Tasks.Records] AS record 
		on [record].[UserId] = [user].[id]
	join dbo.[Tasks.To.Users] AS taskToUser
		on record.UserId = taskToUser.UserId and record.TaskId = taskToUser.TaskId
group by [user].Id, [user].[Name]
