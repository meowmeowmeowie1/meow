<?php

namespace App\Http\Controllers\Admin;

use App\Http\Controllers\Controller;
use App\Models\Category;
use App\Models\Post;
use App\Models\Tag;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Str;
use Illuminate\Validation\Rule;
use Illuminate\View\View;

class PostController extends Controller
{
    public function index(Request $request): View
    {
        $posts = Post::query()
            ->with(['author', 'category'])
            ->when($request->query('locale'), fn ($q, $l) => $q->where('locale', $l))
            ->when($request->query('status'), fn ($q, $s) => $q->where('status', $s))
            ->latest()
            ->paginate(20)
            ->withQueryString();

        return view('admin.posts.index', compact('posts'));
    }

    public function create(): View
    {
        return view('admin.posts.edit', [
            'post' => new Post(['locale' => config('blog.default_locale'), 'status' => 'draft']),
            'categories' => Category::orderBy('name')->get(),
            'tags' => Tag::orderBy('name')->get(),
            'linkCandidates' => collect(),
        ]);
    }

    public function store(Request $request): RedirectResponse
    {
        $data = $this->validateData($request);
        $data['author_id'] = $request->user()->id;
        $data['slug'] = $this->uniqueSlug($data['locale'], $data['slug'] ?? $data['title']);
        $data['reading_minutes'] = $this->estimateReadingMinutes($data['body']);
        $data['published_at'] = $this->resolvePublishedAt($request, $data['status']);

        $post = Post::create($data);
        $post->tags()->sync($this->resolveTagIds($request, $post->locale));

        return redirect()->route('admin.posts.edit', $post)
            ->with('status', 'Post created.');
    }

    public function edit(Post $post): View
    {
        // Candidates to link as the translation counterpart: published posts in
        // the other locale that aren't already in a different translation group.
        $other = $post->locale === 'ar' ? 'en' : 'ar';
        $linkCandidates = Post::where('locale', $other)
            ->where(function ($q) use ($post) {
                $q->whereNull('translation_group_id')
                    ->orWhere('translation_group_id', $post->translation_group_id);
            })
            ->orderBy('title')
            ->get();

        return view('admin.posts.edit', [
            'post' => $post,
            'categories' => Category::orderBy('name')->get(),
            'tags' => Tag::orderBy('name')->get(),
            'linkCandidates' => $linkCandidates,
        ]);
    }

    public function update(Request $request, Post $post): RedirectResponse
    {
        $data = $this->validateData($request, $post);
        $data['slug'] = $this->uniqueSlug($data['locale'], $data['slug'] ?? $data['title'], $post->id);
        $data['reading_minutes'] = $this->estimateReadingMinutes($data['body']);
        $data['published_at'] = $this->resolvePublishedAt($request, $data['status'], $post);

        $post->update($data);
        $post->tags()->sync($this->resolveTagIds($request, $post->locale));

        return redirect()->route('admin.posts.edit', $post)
            ->with('status', 'Post updated.');
    }

    public function destroy(Post $post): RedirectResponse
    {
        $post->delete();

        return redirect()->route('admin.posts.index')->with('status', 'Post deleted.');
    }

    /**
     * Link this post to a counterpart in the other locale so they share a
     * translation_group_id (drives hreflang + the language switcher).
     */
    public function link(Request $request, Post $post): RedirectResponse
    {
        $request->validate(['counterpart_id' => ['nullable', 'exists:posts,id']]);

        if (! $request->filled('counterpart_id')) {
            $post->update(['translation_group_id' => null]);

            return back()->with('status', 'Translation link removed.');
        }

        $counterpart = Post::findOrFail($request->integer('counterpart_id'));
        $group = $post->translation_group_id ?: ($counterpart->translation_group_id ?: (string) Str::uuid());

        $post->update(['translation_group_id' => $group]);
        $counterpart->update(['translation_group_id' => $group]);

        return back()->with('status', 'Translation linked.');
    }

    // ----- Helpers -----

    private function validateData(Request $request, ?Post $post = null): array
    {
        return $request->validate([
            'locale' => ['required', Rule::in(['ar', 'en'])],
            'title' => ['required', 'string', 'max:200'],
            'slug' => ['nullable', 'string', 'max:200'],
            'excerpt' => ['nullable', 'string', 'max:500'],
            'body' => ['required', 'string'],
            'cover_image' => ['nullable', 'string', 'max:255'],
            'category_id' => ['nullable', 'exists:categories,id'],
            'status' => ['required', Rule::in(['draft', 'published'])],
            'is_featured' => ['nullable', 'boolean'],
            'meta_title' => ['nullable', 'string', 'max:200'],
            'meta_description' => ['nullable', 'string', 'max:300'],
            'published_at' => ['nullable', 'date'],
        ]) + ['is_featured' => $request->boolean('is_featured')];
    }

    private function uniqueSlug(string $locale, string $value, ?int $ignoreId = null): string
    {
        $base = Str::slug($value) ?: Str::slug(Str::random(8));
        $slug = $base;
        $i = 2;

        while (
            Post::where('locale', $locale)
                ->where('slug', $slug)
                ->when($ignoreId, fn ($q) => $q->where('id', '!=', $ignoreId))
                ->exists()
        ) {
            $slug = "{$base}-{$i}";
            $i++;
        }

        return $slug;
    }

    private function estimateReadingMinutes(string $body): int
    {
        $words = max(str_word_count(strip_tags($body)), mb_strlen(strip_tags($body)) / 6);

        return max(1, (int) ceil($words / 200));
    }

    private function resolvePublishedAt(Request $request, string $status, ?Post $post = null)
    {
        if ($request->filled('published_at')) {
            return $request->date('published_at');
        }

        if ($status === 'published') {
            return $post?->published_at ?? now();
        }

        return null;
    }

    /**
     * Accept a comma-separated tag string, creating tags in the post's locale
     * as needed, and return their IDs for syncing.
     */
    private function resolveTagIds(Request $request, string $locale): array
    {
        $names = collect(explode(',', (string) $request->input('tags', '')))
            ->map(fn ($t) => trim($t))
            ->filter()
            ->unique();

        return $names->map(function ($name) use ($locale) {
            return Tag::firstOrCreate(
                ['locale' => $locale, 'slug' => Str::slug($name) ?: Str::slug(Str::random(6))],
                ['name' => $name],
            )->id;
        })->all();
    }
}
