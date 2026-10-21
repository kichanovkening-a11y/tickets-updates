import unittest
from release_retention import REQUIRED_ASSETS, retention_plan


def release(tag, **extra):
    return dict(tag_name=tag, published_at='2026-10-21T00:00:00Z',
                assets=[dict(name=name, state='uploaded') for name in REQUIRED_ASSETS], **extra)


class RetentionChecks(unittest.TestCase):
    def test_keep_latest_two_versions_and_remove_only_older_stable(self):
        values = [release('v0.9.0'), release('v0.14.0'), release('v0.12.0'), release('v0.13.0'),
                  release('v0.15.0', prerelease=True), release('v0.16.0', draft=True), release('manual-copy')]
        kept, removed = retention_plan(values)
        self.assertEqual([x['tag_name'] for x in kept], ['v0.14.0', 'v0.13.0'])
        self.assertEqual([x['tag_name'] for x in removed], ['v0.12.0', 'v0.9.0'])

    def test_nothing_deleted_when_only_two_exist(self):
        self.assertEqual(retention_plan([release('v0.14.0'), release('v0.13.0')])[1], [])
        self.assertEqual(retention_plan([]), ([], []))

    def test_do_not_delete_backups_when_new_release_has_missing_asset(self):
        latest = release('v0.14.0')
        latest['assets'] = latest['assets'][:-1]
        with self.assertRaises(ValueError):
            retention_plan([release('v0.12.0'), release('v0.13.0'), latest])


if __name__ == '__main__':
    unittest.main()
