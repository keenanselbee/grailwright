namespace AvalonUntold
{
	public static class KnownGaps
	{
		public static readonly string[] UnmodelledBookmarkCarriers = new string[13]
		{
			"StartWyrdrepellingFireplaceAttachment.TalkConfigs[].dialogue / .dialogueTester (selected at runtime by flag+DLC; the bookmark belongs to a SPAWNED talking location, not to this one)", "ReadStoryAfterAttachment.bookmark", "StonehengeAttachment.storyRef", "RealTimeDelayedStoryAttachment.story", "StartStoryOnConditionAttachment.story", "SpawnerAttachment.storyOnAllKilled / BaseLocationSpawner._storyOnAllKilled", "HideSpotLocationSpawnerAttachment (spawner story)", "TheTowerOfBoneAndTimberCoordinatorAttachment", "PortalOverrideWithStoryAttachment / PortalOverrideWithStory._alternativeStory (Element<Portal>, not Element<Location>)", "DoorsAction._storyOnInteract (ToInitialChapter at Execute time)",
			"LogicEmitterActionBase<T>._attachment.StoryOnInteract (open generic base)", "DeferredActionWithBookmark.Bookmark (a queued deferred action, not a Location element)", "TeleportHeroOnHeroDeath._bookmark (Element<Hero>)"
		};
	}
}
