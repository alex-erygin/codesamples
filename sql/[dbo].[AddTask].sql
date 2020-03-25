create proc [dbo].[AddTask]
	@chunkId uniqueidentifier
as
begin
	declare @taskId uniqueidentifier = newid()

	insert into [dbo].[Tasks] ( [Id], [ChunkId] )
	values ( @taskId, @chunkId )

	select @taskId
end
GO
