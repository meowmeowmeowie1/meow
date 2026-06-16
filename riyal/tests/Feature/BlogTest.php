<?php

namespace Tests\Feature;

use App\Models\Category;
use App\Models\Post;
use App\Models\Setting;
use App\Models\User;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Support\Str;
use Tests\TestCase;

class BlogTest extends TestCase
{
    use RefreshDatabase;

    private function makePair(): array
    {
        $author = User::factory()->create(['role' => 'admin']);
        $group = (string) Str::uuid();

        $ar = Post::create([
            'translation_group_id' => $group,
            'locale' => 'ar',
            'title' => 'مقال تجريبي',
            'slug' => 'tajriba',
            'excerpt' => 'مقتطف',
            'body' => "## عنوان\n\nفقرة أولى.\n\nفقرة ثانية.",
            'author_id' => $author->id,
            'status' => 'published',
            'published_at' => now()->subDay(),
        ]);

        $en = Post::create([
            'translation_group_id' => $group,
            'locale' => 'en',
            'title' => 'Test article',
            'slug' => 'test-article',
            'excerpt' => 'Excerpt',
            'body' => "## Heading\n\nFirst paragraph.\n\nSecond paragraph.",
            'author_id' => $author->id,
            'status' => 'published',
            'published_at' => now()->subDay(),
        ]);

        return [$ar, $en];
    }

    public function test_root_redirects_to_default_locale(): void
    {
        $this->get('/')->assertRedirect('/'.config('blog.default_locale'));
    }

    public function test_arabic_home_renders_rtl(): void
    {
        $this->get('/ar')
            ->assertOk()
            ->assertSee('dir="rtl"', false)
            ->assertSee('lang="ar"', false);
    }

    public function test_english_home_renders_ltr(): void
    {
        $this->get('/en')
            ->assertOk()
            ->assertSee('dir="ltr"', false)
            ->assertSee('lang="en"', false);
    }

    public function test_post_page_has_hreflang_and_jsonld(): void
    {
        [$ar, $en] = $this->makePair();

        $this->get($en->url())
            ->assertOk()
            ->assertSee('Test article')
            ->assertSee('hreflang="ar"', false)
            ->assertSee($ar->url(), false)
            ->assertSee('"@type":"Article"', false)
            ->assertSee('"@type":"BreadcrumbList"', false);
    }

    public function test_draft_posts_are_not_public(): void
    {
        $author = User::factory()->create();
        $post = Post::create([
            'locale' => 'en', 'title' => 'Hidden', 'slug' => 'hidden',
            'body' => 'x', 'author_id' => $author->id, 'status' => 'draft',
        ]);

        $this->get($post->url())->assertNotFound();
    }

    public function test_search_finds_published_posts(): void
    {
        $this->makePair();

        $this->get('/en/search?q=paragraph')
            ->assertOk()
            ->assertSee('Test article');
    }

    public function test_seo_files_are_served(): void
    {
        $this->get('/sitemap.xml')->assertOk()->assertHeader('Content-Type', 'application/xml');
        $this->get('/robots.txt')->assertOk()->assertSee('Disallow: /admin');
        $this->get('/ads.txt')->assertOk();
    }

    public function test_ads_txt_builds_from_publisher_setting(): void
    {
        Setting::set('adsense_client_id', 'ca-pub-1234567890123456');

        $this->get('/ads.txt')
            ->assertOk()
            ->assertSee('google.com, pub-1234567890123456, DIRECT, f08c47fec0942fa0');
    }

    public function test_admin_requires_authentication(): void
    {
        $this->get('/admin')->assertRedirect(route('admin.login'));
    }

    public function test_admin_can_create_post(): void
    {
        $admin = User::factory()->create(['role' => 'admin']);
        Category::create(['locale' => 'en', 'name' => 'News', 'slug' => 'news']);

        $response = $this->actingAs($admin)->post(route('admin.posts.store'), [
            'locale' => 'en',
            'title' => 'My new post',
            'body' => 'Hello world body content.',
            'status' => 'published',
            'tags' => 'money, saving',
        ]);

        $this->assertDatabaseHas('posts', ['title' => 'My new post', 'slug' => 'my-new-post']);
        $post = Post::where('slug', 'my-new-post')->first();
        $response->assertRedirect(route('admin.posts.edit', $post));
        $this->assertCount(2, $post->tags);
    }

    public function test_translation_linking_shares_group_id(): void
    {
        [$ar, $en] = $this->makePair();
        $ar->update(['translation_group_id' => null]);
        $en->update(['translation_group_id' => null]);

        $admin = User::factory()->create(['role' => 'admin']);
        $this->actingAs($admin)->post(route('admin.posts.link', $ar), [
            'counterpart_id' => $en->id,
        ]);

        $this->assertNotNull($ar->fresh()->translation_group_id);
        $this->assertSame($ar->fresh()->translation_group_id, $en->fresh()->translation_group_id);
    }
}
