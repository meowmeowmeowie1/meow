<?php

namespace App\Http\Controllers;

use App\Models\Tag;
use Illuminate\View\View;

class TagController extends Controller
{
    public function show(string $locale, string $slug): View
    {
        $tag = Tag::query()
            ->locale($locale)
            ->where('slug', $slug)
            ->firstOrFail();

        $posts = $tag->posts()
            ->published()
            ->with(['author', 'category'])
            ->latest('published_at')
            ->paginate(9);

        return view('tags.show', [
            'tag' => $tag,
            'posts' => $posts,
            'locale' => $locale,
            'alternates' => [$tag->locale => $tag->url()],
            'title' => $tag->name.' — '.config('app.name'),
        ]);
    }
}
