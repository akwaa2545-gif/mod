from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]
class AftermathAdapterContract(unittest.TestCase):
    def test_read_only_exact_notification_adapter(self):
        path=ROOT/'src/TargetingPodDisplay/AftermathTracker.cs'
        self.assertTrue(path.exists(), 'after-kill read-only adapter is missing')
        text=path.read_text()
        for required in ('UserCode_RpcKillMessage_635947223','postfix:', 'ownerValid()', 'Thread.CurrentThread.ManagedThreadId','DropWatched()', 'Time.unscaledTimeAsDouble', 'harmony.UnpatchSelf()', 'internal bool Pending'):
            self.assertIn(required,text)
        for forbidden in ('prefix:', '.ReportKilled(', '.DisableUnit(', '.ClearTargetList(', '.AddTargetList(', '.Fire(', '.Networkdisabled ='):
            self.assertNotIn(forbidden,text)
if __name__=='__main__':unittest.main()
