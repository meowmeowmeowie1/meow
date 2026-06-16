<?php

namespace App\Http\Controllers;

use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\App;
use Illuminate\View\View;

class PageController extends Controller
{
    public function about(): View
    {
        return view('pages.about', [
            'locale' => App::getLocale(),
            'title' => __('messages.about').' — '.config('app.name'),
        ]);
    }

    public function contact(): View
    {
        return view('pages.contact', [
            'locale' => App::getLocale(),
            'title' => __('messages.contact').' — '.config('app.name'),
        ]);
    }

    public function submitContact(Request $request): RedirectResponse
    {
        $data = $request->validate([
            'name' => ['required', 'string', 'max:120'],
            'email' => ['required', 'email', 'max:160'],
            'message' => ['required', 'string', 'max:5000'],
        ]);

        // v1: log the enquiry. Wire to mail/CRM later.
        logger()->info('Contact form submission', $data);

        return back()->with('status', __('messages.contact_sent'));
    }

    public function privacy(): View
    {
        return view('pages.privacy', [
            'locale' => App::getLocale(),
            'title' => __('messages.privacy').' — '.config('app.name'),
        ]);
    }

    public function terms(): View
    {
        return view('pages.terms', [
            'locale' => App::getLocale(),
            'title' => __('messages.terms').' — '.config('app.name'),
        ]);
    }
}
