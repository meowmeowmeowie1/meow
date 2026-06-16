<?php

namespace App\Http\Controllers;

use App\Models\Category;
use Illuminate\View\View;

class CategoryController extends Controller
{
    public function show(string $locale, string $slug): View
    {
        $category = Category::query()
            ->locale($locale)
            ->where('slug', $slug)
            ->firstOrFail();

        $posts = $category->posts()
            ->published()
            ->with(['author', 'category'])
            ->latest('published_at')
            ->paginate(9);

        $alternates = [$category->locale => $category->url()];
        if ($counterpart = $category->counterpart()) {
            $alternates[$counterpart->locale] = $counterpart->url();
        }

        return view('categories.show', [
            'category' => $category,
            'posts' => $posts,
            'locale' => $locale,
            'alternates' => $alternates,
            'title' => $category->name.' — '.config('app.name'),
            'metaDescription' => $category->description,
        ]);
    }
}
