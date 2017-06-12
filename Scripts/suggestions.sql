CREATE TABLE [Suggestions]
(
	[Suggestion]  INT IDENTITY PRIMARY KEY,
	[Title]       NVARCHAR(120) NOT NULL,
	[IsOrchestra] BIT DEFAULT '0',
	[IsChoir]     BIT DEFAULT '0',
	[CreatorUUN]  NCHAR(8) NOT NULL FOREIGN KEY REFERENCES Members(UUN),
)

CREATE TABLE [Endorsements]
(
	[UUN]        NCHAR(8) NOT NULL FOREIGN KEY REFERENCES Members(UUN),
	[Suggestion] INT      NOT NULL FOREIGN KEY REFERENCES Suggestions(Suggestion),

	PRIMARY KEY (UUN, Suggestion)
)
