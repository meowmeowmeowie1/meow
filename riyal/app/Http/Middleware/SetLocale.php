<?php

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\App;
use Illuminate\Support\Facades\URL;
use Symfony\Component\HttpFoundation\Response;

class SetLocale
{
    /**
     * Resolve the active locale from the route's {locale} prefix, validate it
     * against the configured locales, and make it the app locale for the
     * request. Also pins it as a default for generated URLs so every link in
     * the response stays inside the current language.
     */
    public function handle(Request $request, Closure $next): Response
    {
        $locale = $request->route('locale');
        $supported = array_keys(config('blog.locales'));

        if (! in_array($locale, $supported, true)) {
            $locale = config('blog.default_locale');
        }

        App::setLocale($locale);
        URL::defaults(['locale' => $locale]);

        return $next($request);
    }
}
