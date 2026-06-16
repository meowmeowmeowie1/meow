<?php

namespace App\Http\Controllers;

use App\Models\Category;
use App\Models\Post;
use Illuminate\Support\Facades\App;
use Illuminate\View\View;

class HomeController extends Controller
{
    public function index(): View
    {
        $locale = App::getLocale();

        $featured = Post::query()
            ->locale($locale)
            ->published()
            ->where('is_featured', true)
            ->with(['author', 'category'])
            ->latest('published_at')
            ->take(3)
            ->get();

        $latest = Post::query()
            ->locale($locale)
            ->published()
            ->whereNotIn('id', $featured->pluck('id'))
            ->with(['author', 'category'])
            ->latest('published_at')
            ->take(9)
            ->get();

        $categories = Category::query()
            ->locale($locale)
            ->withCount(['posts' => fn ($q) => $q->published()])
            ->orderByDesc('posts_count')
            ->get();

        return view('home', compact('featured', 'latest', 'categories', 'locale'));
    }
}
