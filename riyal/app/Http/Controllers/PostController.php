<?php

namespace App\Http\Controllers;

use App\Models\Post;
use Illuminate\Support\Facades\App;
use Illuminate\View\View;

class PostController extends Controller
{
    public function show(string $locale, string $slug): View
    {
        $post = Post::query()
            ->locale($locale)
            ->published()
            ->where('slug', $slug)
            ->with(['author', 'category', 'tags'])
            ->firstOrFail();

        // Lightweight view counter (no per-user dedupe in v1).
        $post->incrementQuietly('views');

        $related = Post::query()
            ->locale($locale)
            ->published()
            ->where('id', '!=', $post->id)
            ->when($post->category_id, fn ($q) => $q->where('category_id', $post->category_id))
            ->latest('published_at')
            ->take(3)
            ->get();

        if ($related->count() < 3) {
            $related = $related->concat(
                Post::query()
                    ->locale($locale)
                    ->published()
                    ->where('id', '!=', $post->id)
                    ->whereNotIn('id', $related->pluck('id'))
                    ->latest('published_at')
                    ->take(3 - $related->count())
                    ->get()
            );
        }

        $counterpart = $post->counterpart();

        $alternates = [$post->locale => $post->url()];
        if ($counterpart) {
            $alternates[$counterpart->locale] = $counterpart->url();
        }

        return view('posts.show', [
            'post' => $post,
            'related' => $related,
            'counterpart' => $counterpart,
            'locale' => $locale,
            'alternates' => $alternates,
            'title' => $post->metaTitle().' — '.config('app.name'),
            'metaDescription' => $post->metaDescription(),
            'ogType' => 'article',
            'ogImage' => $post->cover_image,
        ]);
    }
}
