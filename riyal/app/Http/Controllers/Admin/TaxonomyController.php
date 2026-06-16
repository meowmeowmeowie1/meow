<?php

namespace App\Http\Controllers\Admin;

use App\Http\Controllers\Controller;
use App\Models\Category;
use App\Models\Tag;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Str;
use Illuminate\Validation\Rule;
use Illuminate\View\View;

class TaxonomyController extends Controller
{
    public function categories(): View
    {
        return view('admin.categories', [
            'categories' => Category::withCount('posts')->orderBy('locale')->orderBy('name')->get(),
        ]);
    }

    public function storeCategory(Request $request): RedirectResponse
    {
        $data = $request->validate([
            'locale' => ['required', Rule::in(['ar', 'en'])],
            'name' => ['required', 'string', 'max:120'],
            'description' => ['nullable', 'string', 'max:500'],
            'translation_group_id' => ['nullable', 'string'],
        ]);

        $data['slug'] = Str::slug($data['name']) ?: Str::slug(Str::random(6));
        Category::create($data);

        return back()->with('status', 'Category created.');
    }

    public function destroyCategory(Category $category): RedirectResponse
    {
        $category->delete();

        return back()->with('status', 'Category deleted.');
    }

    public function tags(): View
    {
        return view('admin.tags', [
            'tags' => Tag::withCount('posts')->orderBy('locale')->orderBy('name')->get(),
        ]);
    }

    public function storeTag(Request $request): RedirectResponse
    {
        $data = $request->validate([
            'locale' => ['required', Rule::in(['ar', 'en'])],
            'name' => ['required', 'string', 'max:120'],
        ]);

        $data['slug'] = Str::slug($data['name']) ?: Str::slug(Str::random(6));
        Tag::firstOrCreate(['locale' => $data['locale'], 'slug' => $data['slug']], $data);

        return back()->with('status', 'Tag created.');
    }

    public function destroyTag(Tag $tag): RedirectResponse
    {
        $tag->delete();

        return back()->with('status', 'Tag deleted.');
    }
}
