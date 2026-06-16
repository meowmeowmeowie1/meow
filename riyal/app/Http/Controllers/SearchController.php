<?php

namespace App\Http\Controllers;

use App\Models\Post;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\App;
use Illuminate\View\View;

class SearchController extends Controller
{
    public function index(Request $request): View
    {
        $locale = App::getLocale();
        $q = trim((string) $request->query('q', ''));

        $posts = collect();

        if ($q !== '') {
            $posts = Post::query()
                ->locale($locale)
                ->published()
                ->where(function ($query) use ($q) {
                    $query->where('title', 'like', "%{$q}%")
                        ->orWhere('excerpt', 'like', "%{$q}%")
                        ->orWhere('body', 'like', "%{$q}%");
                })
                ->with(['author', 'category'])
                ->latest('published_at')
                ->paginate(9)
                ->withQueryString();
        }

        return view('search', [
            'posts' => $posts,
            'q' => $q,
            'locale' => $locale,
            'alternates' => [$locale => url()->current()],
            'title' => ($q !== '' ? $q.' — ' : '').__('messages.search').' — '.config('app.name'),
        ]);
    }
}
